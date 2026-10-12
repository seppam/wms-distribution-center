using AutoMapper;
using WmsDistributionCenter.Api.Configuration;

namespace WmsDistributionCenter.Tests;

public class MappingTests
{
    [Fact]
    public void MappingProfile_IsValid() =>
        new MapperConfiguration(c => c.AddProfile<MappingProfile>()).AssertConfigurationIsValid();
}
