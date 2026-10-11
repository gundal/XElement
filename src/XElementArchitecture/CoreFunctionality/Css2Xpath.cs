using System.Text.RegularExpressions;

namespace XElementArchitecture.CoreFunctionality;

/// <summary>
/// Small employer-neutral CSS-to-XPath adapter used only when a locator must be
/// composed with an XPath-relative child. The historical framework exposed the
/// same Css2xpath.Transform entry point; this implementation is newly written.
/// </summary>
public static class Css2xpath
{
    private static readonly Regex AttributeSelector = new(
        """^(?<tag>[a-zA-Z][\w-]*|\*)?\[(?<attr>[\w:-]+)(?:\s*=\s*['"]?(?<value>[^'"\]]+)['"]?)?\]$""",
        RegexOptions.Compiled);

    private static readonly Regex IdSelector = new(
        @"^(?<tag>[a-zA-Z][\w-]*|\*)?#(?<id>[\w:-]+)$",
        RegexOptions.Compiled);

    private static readonly Regex ClassSelector = new(
        @"^(?<tag>[a-zA-Z][\w-]*|\*)?\.(?<class>[\w:-]+)$",
        RegexOptions.Compiled);

    public static string Transform(string css)
    {
        if (string.IsNullOrWhiteSpace(css))
            throw new ArgumentException("Locator cannot be empty.", nameof(css));

        css = css.Trim();
        if (css.StartsWith("//", StringComparison.Ordinal) || css.StartsWith("(//", StringComparison.Ordinal))
            return css;

        Match id = IdSelector.Match(css);
        if (id.Success)
        {
            string tag = NormalizeTag(id.Groups["tag"].Value);
            return $"//{tag}[@id={XPathLiteral(id.Groups["id"].Value)}]";
        }

        Match cls = ClassSelector.Match(css);
        if (cls.Success)
        {
            string tag = NormalizeTag(cls.Groups["tag"].Value);
            string value = XPathLiteral($" {cls.Groups["class"].Value} ");
            return $"//{tag}[contains(concat(' ', normalize-space(@class), ' '), {value})]";
        }

        Match attr = AttributeSelector.Match(css);
        if (attr.Success)
        {
            string tag = NormalizeTag(attr.Groups["tag"].Value);
            string name = attr.Groups["attr"].Value;
            if (!attr.Groups["value"].Success)
                return $"//{tag}[@{name}]";
            return $"//{tag}[@{name}={XPathLiteral(attr.Groups["value"].Value)}]";
        }

        if (Regex.IsMatch(css, @"^[a-zA-Z][\w-]*$"))
            return $"//{css}";

        // For complex selectors that cannot safely be converted without a full CSS
        // parser, fail explicitly rather than fabricate a locator.
        throw new NotSupportedException($"CSS selector '{css}' cannot be safely converted to XPath for locator composition.");
    }

    private static string NormalizeTag(string tag) => string.IsNullOrEmpty(tag) ? "*" : tag;

    private static string XPathLiteral(string value)
    {
        if (!value.Contains('\'')) return $"'{value}'";
        if (!value.Contains('"')) return $"\"{value}\"";

        string[] parts = value.Split('\'');
        return "concat(" + string.Join(", \"'\", ", parts.Select(p => $"'{p}'")) + ")";
    }
}
