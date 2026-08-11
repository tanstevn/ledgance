using System.Collections;
using System.Reflection;
using Ledgance.Shared.Infrastructure.Supabase;
using Ledgance.Shared.Infrastructure.Supabase.Models;
using Supabase.Postgrest.Models;

namespace Ledgance.Shared.Unit.Tests.Supabase {
    public class PersistenceModelTests {
        /// <summary>
        /// An insert serializes every property, so a null collection is written as SQL null
        /// instead of letting the column's default apply — which fails against the NOT NULL
        /// jsonb and array columns the schema uses. Every collection property must therefore
        /// start empty.
        /// </summary>
        [Fact]
        public void No_model_starts_with_a_null_collection_property() {
            var models = typeof(SupabaseSettings).Assembly
                .GetTypes()
                .Where(type => type is { IsAbstract: false, IsClass: true }
                    && typeof(BaseModel).IsAssignableFrom(type))
                .ToList();

            Assert.NotEmpty(models);

            foreach (var model in models) {
                var instance = Activator.CreateInstance(model)!;

                var collections = model
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanRead
                        && property.PropertyType != typeof(string)
                        && typeof(IEnumerable).IsAssignableFrom(property.PropertyType));

                foreach (var property in collections) {
                    Assert.True(property.GetValue(instance) is not null,
                        $"{model.Name}.{property.Name} is null on a new instance and would be "
                        + "inserted as SQL null.");
                }
            }
        }
    }
}
