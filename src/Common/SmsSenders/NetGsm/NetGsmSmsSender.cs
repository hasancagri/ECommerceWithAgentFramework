using System.Net;
using System.Text;
using Common.SmsSenders;
using Common.SmsSenders.NetGsm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common;

public class NetGsmSmsSender : ISmsSender
{
    private readonly NetGsmOptions _options;
    private readonly ILogger<NetGsmSmsSender> _logger;

    public NetGsmSmsSender(IOptions<NetGsmOptions> options, ILogger<NetGsmSmsSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<ISmsOutput> SendAsync(ISmsInput input)
    {
        var phone = input.Phone.Replace("+90", "0");
        var ss = string.Empty;
        ss += "<?xml version='1.0' encoding='UTF-8'?>";
        ss += "<mainbody>";
        ss += "<header>";
        ss += "<company dil='TR'>Netgsm</company>";
        ss += $"<usercode>{_options.UserCode}</usercode>";
        ss += $"<password>{_options.Password}</password>";
        ss += "<type>n:n</type>";
        ss += "<msgheader>Moodivation</msgheader>";
        ss += "</header>";
        ss += "<body>";
        ss += "<mp>";
        ss += "<msg><![CDATA[" + input.Message + "]]></msg>";
        ss += "<no>" + phone + "</no>";
        ss += "</mp>";
        ss += "</body>";
        ss += "</mainbody>";

        var res = XmlPost($"{_options.BaseUrl}/sms/send/xml", ss);

        if (_errorMessages.TryGetValue(res, out var message))
        {
            _logger.LogError("[NetGsmSmsSender] error - {message}", message);
            _logger.LogError("[NetGsmSmsSender] error {ss} - response {res}", ss, res);
            return Task.FromResult(NetGsmSmsOutput.Error(message, res));
        }


        return Task.FromResult(NetGsmSmsOutput.Success());
    }

    public class NetGsmSmsOutput : ISmsOutput
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public int? ErrorCode { get; set; }

        public static ISmsOutput Success()
        {
            return new NetGsmSmsOutput
            {
                IsSuccess = true
            };
        }

        public static ISmsOutput Error(string message, string code)
        {
            int? errorCode = int.TryParse(code, out var r) ? r : null;
            return new NetGsmSmsOutput
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = message
            };
        }
    }

    private static string XmlPost(string postAddress, string xmlData)
    {
        try
        {
            using (WebClient wUpload = new())
            {
                var request = WebRequest.Create(postAddress) as HttpWebRequest;
                request!.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                var bPostArray = Encoding.UTF8.GetBytes(xmlData);
                var bResponse = wUpload.UploadData(postAddress, "POST", bPostArray);
                var sReturnChars = Encoding.UTF8.GetChars(bResponse);
                var sWebPage = new string(sReturnChars);

                return sWebPage;
            }
        }
        catch
        {
            return "-1";
        }
    }

    private static Dictionary<string, string> _errorMessages = new()
    {
        { "20", "Mesaj metninde ki problemden dolayı gönderilemediğini veya standart maksimum mesaj karakter sayısını geçtiğini ifade eder." },
        {
            "30",
            "Geçersiz kullanıcı adı , şifre veya kullanıcınızın API erişim izninin olmadığını gösterir.Ayrıca eğer API erişiminizde IP sınırlaması yaptıysanız ve sınırladığınız ip dışında gönderim sağlıyorsanız 30 hata kodunu alırsınız. API erişim izninizi veya IP sınırlamanızı , web arayüzden; sağ üst köşede bulunan Abonelik İşlemleri / API işlemleri menüsunden kontrol edebilirsiniz."
        },
        { "40", "Mesaj başlığınızın (gönderici adınızın) sistemde tanımlı olmadığını ifade eder. Gönderici adlarınızı API ile sorgulayarak kontrol edebilirsiniz." },
        { "50", "Abone hesabınız ile İYS kontrollü gönderimler yapılamamaktadır." },
        { "51", "Aboneliğinize tanımlı İYS Marka bilgisi bulunamadığını ifade eder." },
        { "70", "Hatalı sorgulama. Gönderdiğiniz parametrelerden birisi hatalı veya zorunlu alanlardan birinin eksik olduğunu ifade eder." },
        { "80", "Gönderim sınır aşımı." },
        { "85", "Mükerrer Gönderim sınır aşımı. Aynı numaraya 1 dakika içerisinde 20'den fazla görev oluşturulamaz." },
    };
}
