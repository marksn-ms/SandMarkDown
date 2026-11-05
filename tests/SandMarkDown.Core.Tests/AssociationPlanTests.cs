using SandMarkDown.Core;

namespace SandMarkDown.Core.Tests;

/// <summary>
/// Verifies the per-user Markdown registration.
/// </summary>
public class AssociationPlanTests
{
    /// <summary>
    /// The plan points the file type at the program, offers it for both extensions, and registers for Default apps.
    /// </summary>
    [Fact]
    public void Plan_registers_the_program_for_markdown()
    {
        var entries = AssociationPlan.For(@"C:\Apps\SandMarkDown\SandMarkDown.exe");

        Assert.Contains(entries, e => e.SubKey == @"Software\Classes\SandMarkDown.md\shell\open\command"
            && e.Value == "\"C:\\Apps\\SandMarkDown\\SandMarkDown.exe\" \"%1\"");
        Assert.Contains(entries, e => e.SubKey == @"Software\Classes\.md\OpenWithProgids" && e.Name == "SandMarkDown.md");
        Assert.Contains(entries, e => e.SubKey == @"Software\Classes\.markdown\OpenWithProgids" && e.Name == "SandMarkDown.md");
        Assert.Contains(entries, e => e.SubKey == @"Software\RegisteredApplications"
            && e.Name == AssociationPlan.RegisteredName
            && e.Value == AssociationPlan.CapabilitiesKey);
        Assert.Contains(entries, e => e.SubKey == AssociationPlan.CapabilitiesKey + @"\FileAssociations" && e.Name == ".markdown");
    }

    /// <summary>
    /// The plan never writes the user's default choice or the extension's default value.
    /// </summary>
    [Fact]
    public void Plan_leaves_the_users_default_choice_alone()
    {
        var entries = AssociationPlan.For(@"C:\Apps\SandMarkDown.exe");

        Assert.DoesNotContain(entries, e => e.SubKey.Contains("UserChoice", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e => e.SubKey == @"Software\Classes\.md" && e.Name.Length == 0);
        Assert.All(entries, e => Assert.False(e.SubKey.StartsWith("HKEY", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The program path is read back from quoted and unquoted commands.
    /// </summary>
    [Theory]
    [InlineData("\"C:\\Apps\\SandMarkDown.exe\" \"%1\"", "C:\\Apps\\SandMarkDown.exe")]
    [InlineData("C:\\Apps\\SandMarkDown.exe %1", "C:\\Apps\\SandMarkDown.exe")]
    [InlineData("", null)]
    [InlineData("\"", null)]
    public void Executable_is_read_from_the_command(string command, string? expected)
    {
        Assert.Equal(expected, AssociationPlan.ExecutableFromCommand(command));
    }
}
