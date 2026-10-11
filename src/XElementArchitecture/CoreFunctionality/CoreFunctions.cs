namespace XElementArchitecture.CoreFunctionality;

/// <summary>
/// Deliberately small public example of the layer that sat above UIWrapper.
/// Application-specific business functions are excluded, but the separation is
/// retained: tests/pages can call semantic operations without owning Selenium.
/// </summary>
public sealed class CoreFunctions
{
    private readonly UIWrapper _ui;

    public CoreFunctions(UIWrapper ui) => _ui = ui;

    public void Login(string username, string password)
    {
        XElement user = _ui.FE("username");
        XElement pass = _ui.FE("password");
        XElement submit = _ui.MFE("button[type='submit']");

        user.SendKeys(username, clear: true);
        pass.SendKeys(password, clear: true);
        submit.Click();
    }
}
