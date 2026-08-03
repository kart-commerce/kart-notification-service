using YamlDotNet.RepresentationModel;
using Xunit;

namespace Kart.Notification.ContractTests;

/// <summary>Verifies contracts/api-contract.yaml stays empty - the durable, checkable assertion of architecture.md's Boundary Rationale: this service is consumer-only, no public or internal-ops API.</summary>
public class ApiContractTests
{
    [Fact]
    public void Api_contract_declares_no_paths_and_no_components()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        Assert.Empty(paths.Children);

        var components = (YamlMappingNode)root.Children[new YamlScalarNode("components")];
        Assert.Empty(components.Children);
    }
}
