using Common.SmsSenders;
using Common.SmsSenders.NetGsm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common;

public static class NetGsmExtensions
{
    public static void AddNetGsmSmsSender(this IServiceCollection serviceCollection, IConfigurationSection sectionOptions)
    {
        serviceCollection.Configure<NetGsmOptions>(sectionOptions);
        serviceCollection.AddSingleton<ISmsSender, NetGsmSmsSender>();
    }
}
