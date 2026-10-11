using OpenQA.Selenium;
using XElementArchitecture;

namespace XElementArchitecture.Examples;

public sealed class LoginPage
{
    private readonly UIWrapper _ui;

    public LoginPage(IWebDriver driver) => _ui = Global.Setup(driver);

    private XElement Username => _ui.FE("username");
    private XElement Password => _ui.FE("password");
    private XElement Submit => _ui.MFE("button[type='submit']");

    public void Login(string username, string password)
    {
        Username.SendKeys(username, clear: true);
        Password.SendKeys(password, clear: true);
        Submit.Click();
    }
}
