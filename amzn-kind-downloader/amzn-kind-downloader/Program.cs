using OpenQA.Selenium.Firefox;
using OpenQA.Selenium;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Aspose.Words;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace amzn_kind_downloader
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new amzn_kind_downloader());
        }

        public static void StartExecution(string booklink, string cookiespath, string savein)
        {
            var driver = new FirefoxDriver();

            // install a cookie editor for debugging purposes
            //driver.InstallAddOnFromFile(@"cookie_editor-1.10.1.xpi");

            try
            {
                /*
                * The user will likely have cookies whose domain is 'subdomain.amzn.com', so we use a dummy link
                * 'subdomain.amzn.com/dummylink' in order to be able to set all the cookies without any difficulties.
                * 
                * If we first navigated to 'booklink', we'd have been redirected to something like 'amzn.com' in which
                * the cookies which have 'subdomain.amzn.com' would have been rejected. These cookies do not undermine
                * the goal of this program, so it'd have been sufficiently just not to add these, but we chose the 1st strategy.
                */
                Uri uri = new(booklink);
                string dummyLink = uri.Scheme + "://" + uri.Host + "/" + "dummylink404";


                // navigate to URL
                driver.Navigate().GoToUrl(dummyLink);
            }
            catch (System.UriFormatException)
            {
                driver.Quit();
                MessageBox.Show("Seems you did not provide a valid URL.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (OpenQA.Selenium.WebDriverArgumentException exc)
            {
                driver.Quit();
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            driver.Manage().Cookies.DeleteAllCookies();

            List<Cookie> cookies = CookiesFromJson(cookiespath);

            foreach (Cookie cookie in cookies)
            {
                try
                {
                    driver.Manage().Cookies.AddCookie(cookie);
                }
                catch (Exception exc)
                {
                    Console.WriteLine("Add of the cookie " + cookie.Name + " failed: " + exc.Message);
                }
            }

            driver.Navigate().GoToUrl(booklink);

            TurnBookPages(driver, savein);

            string title = FindBookTitle(driver);

            ImagesToPdf(savein, title);

            driver.Quit();

        }

        private static List<Cookie> CookiesFromJson(string cookiespath)
        {
            List<Cookie> cookies = new();
            try
            {
                string json = File.ReadAllText(cookiespath);
                JArray array = JArray.Parse(json);

                string? name, value, domain, path, samesite;
                bool? secure, httponly;
                DateTime? expiry_datetime;
                long? expiry_seconds;

                Cookie newcookie;

                foreach (JObject cookie_json in array)
                {

                    name = cookie_json.GetValue("name", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                    value = cookie_json.GetValue("value", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                    domain = cookie_json.GetValue("domain", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                    path = cookie_json.GetValue("path", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                    expiry_seconds = cookie_json.GetValue("expirationDate", StringComparison.OrdinalIgnoreCase)?.Value<long>();
                    secure = cookie_json.GetValue("secure", StringComparison.OrdinalIgnoreCase)?.Value<bool>();
                    httponly = cookie_json.GetValue("httpOnly", StringComparison.OrdinalIgnoreCase)?.Value<bool>();
                    samesite = cookie_json.GetValue("sameSite", StringComparison.OrdinalIgnoreCase)?.Value<string>();

                    if (samesite is not null)
                    {
                        if (samesite.Equals("no_restriction"))
                        {
                            samesite = "None";
                        }
                    }
                    else
                    {
                        // as specified by the standard [https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Set-Cookie/SameSite], "Lax" is the default
                        samesite = "Lax";
                    }

                    if (expiry_seconds is not null)
                        expiry_datetime = DateTimeOffset.FromUnixTimeSeconds((long)expiry_seconds).DateTime;
                    else
                        expiry_datetime = DateTime.Now.AddDays(1);


                    newcookie = new(name, value, domain, path, expiry_datetime, (bool)secure, (bool)httponly, samesite);
                    cookies.Add(newcookie);
                }

            }
            catch (IOException ioexc)
            {
                Console.WriteLine("Error reading " + cookiespath + ". Error: " + ioexc.Message);
            }
            catch (JsonReaderException jsonexc)
            {
                Console.WriteLine("Error while attempting to read the JSON file: " + jsonexc.Message);
            }

            return cookies;
        }

        private static void TurnBookPages(FirefoxDriver? driver, string savein)
        {
            if (driver is null)
                return;

            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(15));

            driver.Manage().Window.Maximize();
            wait.Until(ExpectedConditions.InvisibilityOfElementLocated(By.XPath("//div[@class='kg-loader']")));
            Thread.Sleep(10000); // users chooses page to start from here (MAX 10 sec)

            // remove cookie policy banner
            try
            {
                wait.Until(ExpectedConditions.ElementIsVisible(By.Id("ion-overlay-1")));
            }
            catch (WebDriverTimeoutException) { }
            driver.ExecuteScript("document.getElementById('ion-overlay-1').remove();");

            // wait until loader is not present
            wait.Until(ExpectedConditions.InvisibilityOfElementLocated(By.XPath("//div[@class='kg-loader']")));

            // somehow amz does not allow to turn the next page unless you go to the previous page first...
            driver.FindElement(By.TagName("html")).SendKeys(OpenQA.Selenium.Keys.ArrowLeft);
            // wait until loader is not present
            wait.Until(ExpectedConditions.InvisibilityOfElementLocated(By.XPath("//div[@class='kg-loader']")));
            driver.FindElement(By.TagName("html")).SendKeys(OpenQA.Selenium.Keys.ArrowRight);

            int pagecounter = 1;
            Screenshot screenshot;

            wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@class='chevron-container right']")));

            while (IsElementPresent(driver, By.XPath("//div[@class='chevron-container right']")))
            {
                // The "Last read page" popup might be present. We need to get rid of it in order to get a proper screenshot
                if (IsElementPresent(driver, By.XPath("//div[contains(@class, 'alert-wrapper')]")))
                {
                    driver.FindElement(By.XPath("//button[contains(@class, 'alert-button ion-focusable ion-activatable')]")).Click();
                }

                screenshot = driver.GetScreenshot();
                try
                {
                    screenshot.SaveAsFile(Path.Combine(savein, pagecounter++.ToString() + ".png"), ScreenshotImageFormat.Png);
                }
                catch (IOException ioexc)
                {
                    driver.Quit();
                    MessageBox.Show("Unable to save the screenshot, operation aborted. Details: " + ioexc.Message, "Operation aborted", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // wait until loader (spinning circle) is not present
                wait.Until(ExpectedConditions.InvisibilityOfElementLocated(By.XPath("//div[@class='kg-loader']")));

                // next page
                wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@class='chevron-container right']")));
                driver.FindElement(By.XPath("//div[@class='chevron-container right']")).Click();
            }
            driver.ExecuteAsyncScript("alert('We are elaborating your PDF. This window will close automatically :-)');");
        }

        private static bool IsElementPresent(FirefoxDriver? driver, By by)
        {
            if (driver is null)
                throw new ArgumentException("driver must be set");
            try
            {
                driver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        private static string FindBookTitle(FirefoxDriver? driver)
        {
            if (driver is null)
                return string.Empty;

            if (IsElementPresent(driver, By.XPath("//div[contains(@class, 'fixed-book-title')]")))
            {
                return driver.FindElement(By.XPath("//div[contains(@class, 'fixed-book-title')]")).Text;
            }
            else
            {
                return "DOWNLOADED_BOOK";
            }
        }

        private static void ImagesToPdf(string savein, string title)
        {
            if (!Directory.Exists(savein))
                throw new Exception("Unable to access " + savein + ". Conversion to PDF aborted.");

            int pagecounter = 1;
            var doc = new Document();
            var builder = new DocumentBuilder(doc);

            // we use the first image to set the PDF height and width
            var imageSizeController = new Bitmap(Path.Combine(savein, pagecounter + ".png"));
            int pdfheight = imageSizeController.Height;
            int pdfwidth = imageSizeController.Width;
            imageSizeController.Dispose();

            builder.PageSetup.PageWidth = pdfwidth;
            builder.PageSetup.PageHeight = pdfheight;

            while (File.Exists(Path.Combine(savein, pagecounter.ToString() + ".png")))
            {
                builder.InsertImage(Path.Combine(savein, pagecounter.ToString() + ".png"));
                pagecounter++;
            }
            doc.Save(Path.Combine(savein, title + ".pdf"));

            pagecounter = 1;
            while (File.Exists(Path.Combine(savein, pagecounter.ToString() + ".png")))
            {
                File.Delete(Path.Combine(savein, pagecounter.ToString() + ".png"));
                pagecounter++;
            }


        }

    }
}