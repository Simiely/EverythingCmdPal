using System.Text.Json.Nodes;

namespace CmdPalLinks.Pages;

internal static class ItemFormTemplate
{
    public static string Build(string kind, string target, string title, string aliases, string tags)
    {
        var card = new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Input.ChoiceSet",
                    ["id"] = "kind",
                    ["label"] = "类型",
                    ["value"] = kind,
                    ["choices"] = new JsonArray
                    {
                        new JsonObject { ["title"] = "网址 (URL)", ["value"] = "url" },
                        new JsonObject { ["title"] = "文件", ["value"] = "file" },
                        new JsonObject { ["title"] = "文件夹", ["value"] = "folder" },
                    },
                },
                TextInput("target", "网址或路径", target, required: true),
                TextInput("title", "标题（选填）", title),
                TextInput("aliases", "别名（选填，逗号分隔）", aliases),
                TextInput("tags", "标签（选填，逗号分隔）", tags),
            },
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Action.Submit",
                    ["title"] = "保存",
                    ["data"] = new JsonObject
                    {
                        ["kind"] = "kind",
                        ["target"] = "target",
                        ["title"] = "title",
                        ["aliases"] = "aliases",
                        ["tags"] = "tags",
                    },
                },
            },
        };

        return card.ToJsonString();
    }

    private static JsonObject TextInput(string id, string label, string value, bool required = false)
    {
        var input = new JsonObject
        {
            ["type"] = "Input.Text",
            ["style"] = "text",
            ["id"] = id,
            ["label"] = label,
            ["value"] = value,
        };

        if (required)
        {
            input["isRequired"] = true;
            input["errorMessage"] = "请填写网址或路径";
            input["placeholder"] = "https://example.com 或 C:\\Users\\me\\file.pdf";
        }

        return input;
    }
}
