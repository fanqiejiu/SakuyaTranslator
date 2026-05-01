namespace MtTransTool.Core.Models;

public sealed class PromptTemplate
{
    public string Name { get; set; } = "默认";
    public string Content { get; set; } = "";
    public bool IsActive { get; set; }
}
