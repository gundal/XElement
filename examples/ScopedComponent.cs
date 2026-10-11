using OpenQA.Selenium;
using XElementArchitecture;

namespace XElementArchitecture.Examples;

public sealed class UserRow
{
    private readonly XElement _row;

    public UserRow(UIWrapper ui, string userId)
    {
        _row = ui.MFE($"[data-user-id='{userId}']");
    }

    public void Edit()
    {
        XElement editButton = _row.FindElement("//*[@data-action='edit']");
        editButton.Click();
    }
}
