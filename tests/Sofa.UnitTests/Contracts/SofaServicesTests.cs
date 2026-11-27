using Sofa.Contracts;

namespace Sofa.UnitTests.Contracts;

public class SofaServicesTests
{
    [Fact]
    public void Names_the_eleven_services_of_ADR_0012_once_each()
    {
        Assert.Equal(11, SofaServices.All.Count);
        Assert.Equal(SofaServices.All.Count, SofaServices.All.Distinct().Count());
    }

    [Theory]
    [InlineData(SofaServices.AppGateway, "app_gateway")]
    [InlineData(SofaServices.Flow, "flow")]
    public void A_services_schema_is_its_name_as_a_postgres_identifier(string service, string schema) =>
        Assert.Equal(schema, SofaServices.SchemaOf(service));
}
