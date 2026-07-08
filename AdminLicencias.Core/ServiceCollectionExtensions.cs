using AdminLicencias.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AdminLicencias.Core.Options;

namespace AdminLicencias.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminLicenciasCore(this IServiceCollection services, Action<LicensingOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.AddOptions<LicensingOptions>();

        services.AddSingleton<LicenseService>();
        services.AddSingleton<DataStore>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LicensingOptions>>().Value;
            var store = new DataStore(opts);
            store.Cargar();
            return store;
        });

        return services;
    }
}
