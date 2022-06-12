using OpenQA.Selenium.Firefox;
using OpenQA.Selenium;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Aspose.Words;

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
                driver.Manage().Cookies.AddCookie(cookie);
            }

            driver.Navigate().GoToUrl(booklink);

            // wait until page is loaded; TODO: generalize
            Thread.Sleep(10000);

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

            driver.Manage().Window.Maximize();
            Thread.Sleep(10000);

            // remove cookie policy banner
            driver.ExecuteScript("document.getElementById('ion-overlay-1').remove();");

            // somehow amz does not show the right button unless you stroke the left button first...
            driver.FindElement(By.TagName("html")).SendKeys(OpenQA.Selenium.Keys.ArrowLeft);
            Thread.Sleep(7000);
            driver.FindElement(By.XPath("html")).SendKeys(OpenQA.Selenium.Keys.ArrowRight);

            int pagecounter = 1;
            Screenshot screenshot;


            while (IsElementPresent(driver, By.XPath("//div[@class='chevron-container right']")))
            {
                screenshot = driver.GetScreenshot();
                screenshot.SaveAsFile(Path.Combine(savein, pagecounter++.ToString() + ".png"), ScreenshotImageFormat.Jpeg);

                // next page
                driver.FindElement(By.XPath("//div[@class='chevron-container right']")).Click();
                Thread.Sleep(2000);
            }
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

            builder.PageSetup.PageWidth = pdfwidth;
            builder.PageSetup.PageHeight = pdfheight;

            while (File.Exists(Path.Combine(savein, pagecounter.ToString() + ".png")))
            {
                builder.InsertImage(Path.Combine(savein, pagecounter.ToString() + ".png"));
                File.Delete(Path.Combine(savein, pagecounter.ToString() + ".png"));
                pagecounter++;
            }

            doc.Save(Path.Combine(savein, title + ".pdf"));
        }

    }
}