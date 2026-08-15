using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Lore.Common.Extensions;

using Shouldly;

namespace Lore.UnitTests.Common;

public class ExtensionTests
{
    public enum SampleEnum
    {
        [Description("option 1 description")]
        [DefaultValue("option one default value")]
        OptionOne,

        OptionTwo
    }

    private sealed record SampleDto(string Name, int Value);

    [Test]
    public void Extract_AttributeFromEnum()
    {
        var optionOneDescription = SampleEnum.OptionOne.GetAttribute<DescriptionAttribute>();
        
        optionOneDescription?.Description.ShouldBe("option 1 description");
    }

    [Test]
    public void GetAttribute_ReturnsDefaultValue_WhenPresent()
    {
        var defaultValue = SampleEnum.OptionOne.GetAttribute<DefaultValueAttribute>();

        defaultValue?.Value.ShouldBe("option one default value");
    }

    [Test]
    public void GetAttribute_ReturnsNull_WhenAttributeAbsent()
    {
        var description = SampleEnum.OptionTwo.GetAttribute<DescriptionAttribute>();

        description.ShouldBe(null);
    }

    [Test]
    public void GetAttribute_ReturnsNull_ForUnrelatedAttributeType()
    {
        var obsolete = SampleEnum.OptionOne.GetAttribute<ObsoleteAttribute>();

        obsolete.ShouldBe(null);
    }

    [Test]
    public void DeserializeJson_ParsesObject_WithMatchingPropertyNames()
    {
        const string json = """{"Name":"alpha","Value":7}""";

        var result = json.DeserializeJson<SampleDto>();

        result!.Name.ShouldBe("alpha");
        result.Value.ShouldBe(7);
    }

    [Test]
    public void DeserializeJson_MatchesProperties_CaseInsensitively_ByDefault()
    {
        const string json = """{"name":"beta","value":9}""";

        var result = json.DeserializeJson<SampleDto>();

        result!.Name.ShouldBe("beta");
        result.Value.ShouldBe(9);
    }

    [Test]
    public void DeserializeJson_RespectsCustomOptions()
    {
        const string json = """{"name":"gamma","value":3}""";
        var strictOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = false };

        var result = json.DeserializeJson<SampleDto>(strictOptions);

        // Strict case matching leaves "name"/"value" unbound, so the fields keep their defaults.
        result!.Name.ShouldBe(null);
        result.Value.ShouldBe(0);
    }

    [Test]
    public void DeserializeJson_ParsesPrimitiveValue()
    {
        const string json = "42";

        var result = json.DeserializeJson<int>();

        result.ShouldBe(42);
    }

    [Test]
    [Arguments("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [Arguments("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [Arguments("hello", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    public async Task ComputeSha256HexAsync_ReturnsKnownHashes(string input, string expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var hash = await stream.ComputeSha256HexAsync();

        hash.ShouldBe(expected);
    }
}
