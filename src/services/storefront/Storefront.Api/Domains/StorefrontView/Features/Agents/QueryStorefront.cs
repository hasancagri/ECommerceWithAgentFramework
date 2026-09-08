using System.Diagnostics;
using Npgsql;

namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// 069: tek serbest-sorgu kapısı. Akış (R2/R3): {{EMBED}} ikamesi → saf bekçi → (geçerse) embedding
// üretimi → kısıtlı rol bağlantısında statement_timeout ile çalıştırma → ret DAHİL her yolda
// AgentQueryLog (sahip oturum). Yapısal zırh kısıtlı roldedir (TEK yetki view SELECT'i, FR-005);
// bekçi ucuz ön-kapı + makine-okur ret kodudur (FR-007 düzeltme döngüsü).
public static class QueryStorefrontForAgent
{
    public record QueryStorefrontQuery(string Sql);

    // R5: serbest SELECT listesi sabit DTO ile temsil edilemez — kolon-adlı satırlar döner.
    // vector kolonları AYIKLANIR (embedding asla LLM'e dönmez; token + sızıntı).
    public class QueryStorefrontResponse
    {
        public bool Ok { get; set; }
        public List<Dictionary<string, object?>> Rows { get; set; } = [];
        public int RowCount { get; set; }
        public bool Truncated { get; set; }
    }

    public class QueryStorefrontQueryHandler
    {
        public async Task<FeatureObjectResultModel<QueryStorefrontResponse>> Handle(
            QueryStorefrontQuery query,
            AgentQueryConnectionSource restricted,
            IDocumentStore store,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            AgentQueryOption options,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var rawSql = query.Sql ?? string.Empty;

            // 1) {{EMBED}} ikamesi (bekçiden ÖNCE — bekçi ikameli SQL'i görür, kontrat sırası).
            var substitution = EmbedPlaceholder.Substitute(rawSql);
            if (!substitution.IsSuccess)
                return await Reject(store, rawSql, substitution.Messages!, sw, ct);

            // 2) Saf bekçi: tek SELECT/WITH, yasak kelime, ilişki whitelist, uzunluk, LIMIT sarmalama.
            var guarded = AgentSqlGuard.Validate(substitution.Data!.Sql, options.MaxSqlLength, options.MaxRows);
            if (!guarded.IsSuccess)
                return await Reject(store, rawSql, guarded.Messages!, sw, ct);

            // 3) Embedding yalnız yer-tutucu varsa (reddedilecek sorguya OpenAI parası harcanmaz, R3).
            var vectorLiterals = new List<string>();
            if (substitution.Data.Texts.Count > 0)
            {
                try
                {
                    var embeddings = await embeddingGenerator.GenerateAsync(
                        substitution.Data.Texts, cancellationToken: ct);
                    vectorLiterals.AddRange(
                        embeddings.Select(e => EmbedPlaceholder.ToVectorLiteral(e.Vector.Span)));
                }
                catch (Exception ex)
                {
                    var message = new MessageItem
                    {
                        Code = StorefrontResourceConstants.STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE,
                        Property = "embedding service unavailable"
                    };
                    await WriteLog(store, AgentQueryLog.Failed(rawSql, message.Code!, ex.Message,
                        (int)sw.ElapsedMilliseconds), ct);
                    return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
                }
            }

            // 4) Kısıtlı bağlantıda çalıştır: SET LOCAL statement_timeout işlem-yerel (FR-004 süre tavanı).
            try
            {
                var response = await Execute(restricted, guarded.Data!.WrappedSql, vectorLiterals, options, ct);
                await WriteLog(store, AgentQueryLog.Executed(rawSql, response.RowCount, response.Truncated,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Ok(response);
            }
            catch (PostgresException pex) when (pex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                // Rol katmanı reddi: bekçiyi geçen view-dışı erişim burada İMKANSIZ kılınır (yapısal).
                var message = new MessageItem
                {
                    Code = StorefrontResourceConstants.AgentSqlPermissionDenied,
                    Property = FirstLine(pex.MessageText)
                };
                await WriteLog(store, AgentQueryLog.Rejected(rawSql, message.Code!, pex.Message,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
            }
            catch (PostgresException pex) when (pex.SqlState == PostgresErrorCodes.QueryCanceled)
            {
                var message = new MessageItem
                {
                    Code = StorefrontResourceConstants.AgentSqlTimeout,
                    Property = $"statement_timeout {options.TimeoutSeconds}s exceeded"
                };
                await WriteLog(store, AgentQueryLog.Failed(rawSql, message.Code!, pex.Message,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
            }
            catch (PostgresException pex)
            {
                // R5: asistana budanmış tek satır (SQLSTATE + mesaj) — tam metin yalnız izde.
                var message = new MessageItem
                {
                    Code = StorefrontResourceConstants.AgentSqlExecutionFailed,
                    Property = $"{pex.SqlState}: {FirstLine(pex.MessageText)}"
                };
                await WriteLog(store, AgentQueryLog.Failed(rawSql, message.Code!, pex.Message,
                    (int)sw.ElapsedMilliseconds), ct);
                return FeatureObjectResultModel<QueryStorefrontResponse>.Error(message);
            }
        }

        private static async Task<QueryStorefrontResponse> Execute(
            AgentQueryConnectionSource restricted,
            string wrappedSql,
            List<string> vectorLiterals,
            AgentQueryOption options,
            CancellationToken ct)
        {
            await using var conn = await restricted.DataSource.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            await using (var timeoutCmd = conn.CreateCommand())
            {
                // Değer config int'i — literal enjeksiyonu güvenli (SET LOCAL parametre bind edemez).
                timeoutCmd.CommandText = $"set local statement_timeout = {options.TimeoutSeconds * 1000}";
                timeoutCmd.Transaction = tx;
                await timeoutCmd.ExecuteNonQueryAsync(ct);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = wrappedSql;
            cmd.Transaction = tx;
            for (var i = 0; i < vectorLiterals.Count; i++)
                cmd.Parameters.AddWithValue($"emb{i}", vectorLiterals[i]);

            var rows = new List<Dictionary<string, object?>>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        // vector kolonları yanıttan ayıklanır (R5).
                        if (reader.GetDataTypeName(i).Contains("vector", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var value = await reader.GetFieldValueAsync<object>(i, ct);
                        row[reader.GetName(i)] = value is DBNull ? null : value;
                    }
                    rows.Add(row);
                }
            }

            await tx.CommitAsync(ct);

            // Sarmalanmış LIMIT = MaxRows+1: fazla satır geldiyse toplam tavandan büyük (R4).
            var truncated = rows.Count > options.MaxRows;
            var rowCount = rows.Count;
            if (truncated)
                rows.RemoveRange(options.MaxRows, rows.Count - options.MaxRows);

            return new QueryStorefrontResponse
            {
                Ok = true,
                Rows = rows,
                RowCount = rowCount,
                Truncated = truncated
            };
        }

        private static async Task<FeatureObjectResultModel<QueryStorefrontResponse>> Reject(
            IDocumentStore store, string rawSql, List<MessageItem> messages, Stopwatch sw, CancellationToken ct)
        {
            var first = messages[0];
            await WriteLog(store, AgentQueryLog.Rejected(rawSql, first.Code!, first.Property,
                (int)sw.ElapsedMilliseconds), ct);
            return FeatureObjectResultModel<QueryStorefrontResponse>.Error(messages);
        }

        // R6: iz SAHİP oturumla yazılır (kısıtlı rol view dışına yazamaz — kanallar ayrık).
        private static async Task WriteLog(IDocumentStore store, AgentQueryLog log, CancellationToken ct)
        {
            await using var session = store.LightweightSession();
            session.Store(log);
            await session.SaveChangesAsync(ct);
        }

        private static string FirstLine(string text)
        {
            var newline = text.IndexOf('\n');
            return newline < 0 ? text : text[..newline];
        }
    }
}
