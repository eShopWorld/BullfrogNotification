using System;
using Autofac;
using ISummonNoobsBackendService;
using Xunit;

namespace ISummonNoobs.Unit.Tests
{
    public class AutofacTestFixture : IDisposable
    {
        public IContainer Container;

        public AutofacTestFixture()
        {
            Setup();
        }

        private void Setup()
        {
            var cb = new ContainerBuilder();
            cb.RegisterModule<CoreModule>();
            Container = cb.Build();
        }

        public void Dispose()
        {
            Container?.Dispose();
        }
    }

    [CollectionDefinition(nameof(AutofacTestFixtureCollection))]
    // ReSharper disable once InconsistentNaming
    public class AutofacTestFixtureCollection : ICollectionFixture<AutofacTestFixture>
    { }
}
