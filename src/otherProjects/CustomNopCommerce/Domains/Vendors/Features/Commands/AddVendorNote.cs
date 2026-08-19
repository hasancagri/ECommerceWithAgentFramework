namespace CustomNopCommerce.Domains.Vendors.Features.Commands;

/// <summary>Satıcıya admin notu ekleme write-slice'ı.</summary>
public static class AddVendorNote
{
    public record AddVendorNoteCommand(Guid VendorId, string Note);

    [Transactional]
    public class AddVendorNoteCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            AddVendorNoteCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var vendor = await session.LoadAsync<Vendor>(cmd.VendorId, ct);
            if (vendor is null || vendor.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = vendor.AddNote(cmd.Note, DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(vendor);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class AddVendorNoteCommandEndpoint
{
    public static RouteGroupBuilder AddVendorNoteGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/notes", async (Guid id,
            [FromBody] AddVendorNote.AddVendorNoteCommand body, IMessageBus bus) =>
            {
                var cmd = body with { VendorId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddVendorNote");
        return group;
    }
}
