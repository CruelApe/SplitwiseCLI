using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace SplitwiseCLI.Cli;

// Thin adapter from Spectre.Console.Cli's ITypeRegistrar to Microsoft.Extensions.DependencyInjection.
// Spectre resolves both its own internal services and our commands through this interface, so a
// full container is needed here rather than a hand-rolled instance store.
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());

    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) => services.AddSingleton(service, _ => factory());
}

public sealed class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);

    public void Dispose() => provider.Dispose();
}
