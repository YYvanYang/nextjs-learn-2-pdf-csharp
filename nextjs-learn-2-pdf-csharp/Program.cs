using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;
using System;
using static System.Collections.Specialized.BitVector32;

class Program
{
    public static async Task Main(string[] args)
    {
        string pdfFolderPath = PrepareDirectory();

        List<NavLink> navLinks = await GetNavLinks();
        var activeLinkSiblings = await GetActiveLinkSiblings(navLinks);
        await GetPagesContent(activeLinkSiblings, pdfFolderPath);

        SaveAllToPdf(pdfFolderPath);
        
    }

    static void SaveAllToPdf(string pdfFolderPath)
    {
        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var sortedPdfFiles = pdfFiles.OrderBy(f => GetFileNumber(f)).ToList();
        // 打印排序后的文件名
        sortedPdfFiles.ForEach(f => Console.WriteLine(f));
        var finalFile = Path.Combine(pdfFolderPath, "react-deep-dive.pdf");
        MergePdfFiles(sortedPdfFiles, finalFile);
    }

    public struct NavLink
    {
        public string Title;
        public string Url;
    }

    public struct ActiveLinkSiblings
    {
        public string ActiveTitle;
        public List<(string Text, string Url)> SiblingLinks;
    }

    public struct PageContent
    {
        public string Title;
        public string Content;
    }

    public struct Link
    {
        public string Text;
        public string Url;
    }

    public static async Task LoadAllPageContent(IPage page)
    {
        // Get the initial scroll height of the page
        var previousScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
        var totalHeight = 0;
        var distance = 300;
        while (true)
        {
            // Scroll down by increasing the scrollTop property of the page's body
            //await page.EvaluateExpressionAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.EvaluateExpressionAsync($"window.scrollBy(0, {distance})");

            // Wait for a little bit for new content to load
            await Task.Delay(500);
            totalHeight += distance;

            // Check if the scroll height has increased, indicating new content has loaded
            var newScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
            Console.WriteLine($"totalHeight: {totalHeight}");
            Console.WriteLine($"newScrollHeight: {newScrollHeight}");
            if (newScrollHeight < totalHeight)
            {
                // No new content has loaded, break out of the loop
                break;
            }


        }
    }

    public static async Task<List<NavLink>> GetNavLinks()
    {
        // Launch browser
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GoToAsync("https://learn.react-js.dev/");

        var sideNavSections = await page.QuerySelectorAllAsync("nav a");
        var navLinks = new List<NavLink>();

        foreach (var section in sideNavSections)
        {
            var header = await section.QuerySelectorAsync("div[dir='auto']");
            var title = await (await header.GetPropertyAsync("textContent")).JsonValueAsync<string>();

            var link = section;

            var url = await (await link.GetPropertyAsync("href")).JsonValueAsync<string>();

            navLinks.Add(new NavLink
            {
                Title = title,
                Url = url
            });
        }

        await browser.CloseAsync();

        foreach (var navLink in navLinks)
        {
            Console.WriteLine($"{navLink.Title} - {navLink.Url}");
        }

        return navLinks;
    }

    public static async Task<List<ActiveLinkSiblings>> GetActiveLinkSiblings(List<NavLink> navLinks)
    {
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Headless = true
        });
        var activeLinkSiblingsList = new List<ActiveLinkSiblings>();

        foreach (var navLink in navLinks)
        {
            var Title = navLink.Title;
            var Url = navLink.Url;
            {
                try
                {

                    var page = await browser.NewPageAsync();
                    await page.GoToAsync(Url);

                    //var activeLink = await page.QuerySelectorAsync(".scroll-link.side-nav-item.active.active-exact");

                    var _url = Url.Replace("https://learn.react-js.dev", "");

                    var activeLink = await page.QuerySelectorAsync($"a[href='{_url}']");
                    if (activeLink != null)
                    {
                        var activeTitle = await (await activeLink.GetPropertyAsync("textContent")).JsonValueAsync<string>();
                        var activeLinkHref = await (await activeLink.GetPropertyAsync("href")).JsonValueAsync<string>();

                        Console.WriteLine($"get sublinks of {activeTitle} {activeLinkHref}");   
                        // here must declar Link[] type, else it will convert fail
                        var siblingLinks = await activeLink.EvaluateFunctionAsync<string[]>(@"(activeLink) => {
                        const siblings = [];
                        let sibling = activeLink.nextElementSibling;
                        if (sibling) {
                            let subLinks = sibling.querySelectorAll('a');
                            for (let l of subLinks) {
                                siblings.push(l.href);
                            }
                        }
                        return siblings;
                    }", activeLink);

                        var links = new List<ActiveLinkSiblings>();

                        if (siblingLinks != null && siblingLinks.Length > 0)
                        {
                            var _siblingLinks = siblingLinks.Select(link => (link, link)).ToList();
                            // add active link to the first position
                            _siblingLinks.Insert(0, (activeTitle, activeLinkHref));
                            activeLinkSiblingsList.Add(new ActiveLinkSiblings
                            {
                                ActiveTitle = activeTitle,
                                SiblingLinks = _siblingLinks
                            });

                        }



                    }

                    await page.CloseAsync();
                }
                catch (Exception)
                {

                }
            }
        }

        await browser.CloseAsync();

        Console.WriteLine();
        Console.WriteLine("========Final links========");
        Console.WriteLine();

        foreach (var navLink in activeLinkSiblingsList)
        {
            Console.WriteLine(navLink.ActiveTitle);
            foreach (var (Text, Url) in navLink.SiblingLinks)
            {
                Console.WriteLine($"{Text} - {Url}");
            }
            Console.WriteLine();
            Console.WriteLine("===================");
            Console.WriteLine();
        }

        return activeLinkSiblingsList;
    }

    public static async Task<List<PageContent>> GetPagesContent(List<ActiveLinkSiblings> activeLinkSiblingsList, string pdfFolderPath)
    {
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Headless = true
        });

        // Set up PDF options
        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            MarginOptions = new MarginOptions
            {
                Top = "2cm",
                Right = "1cm",
                Bottom = "2cm",
                Left = "1cm"
            },
            DisplayHeaderFooter = false,
            PrintBackground = true
        };

        var pagesContentList = new List<PageContent>();
        var index = 0;
        foreach (var activeLinkSiblings in activeLinkSiblingsList)
        {
            foreach (var (Text, Url) in activeLinkSiblings.SiblingLinks)
            {
                var page = await browser.NewPageAsync();
                await page.GoToAsync(Url);

                try
                {
                    Console.WriteLine($"fetching page: {Url}");

                    Console.WriteLine("Scrolling page first time");
                    await LoadAllPageContent(page);
                    Console.WriteLine("Scrolling page second time");
                    await LoadAllPageContent(page);

                    await HideHeaderAndFooter(page);

                    // Save PDF to the created folder
                    var pdfFilePath = Path.Combine(pdfFolderPath, $"Article_{index}.pdf");
                    // Save content to PDF
                    await page.PdfAsync(pdfFilePath, pdfOptions);

                    index++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                await page.CloseAsync();
            }
        }

        await browser.CloseAsync();
        return pagesContentList;
    }

    static async Task HideHeaderAndFooter(IPage page)
    {
        // Set header and footer elements to null
        await page.EvaluateFunctionAsync(@"() => {
            var header = document.querySelector('header');
            var pheader = document.querySelector('.pheader');
            var aside = document.querySelector('aside');
            var docsNav = document.querySelector('.docs-nav');
            var footer = document.querySelector('footer');
            var cconsentBar = document.querySelector('#cconsent-bar');
            var cconsentModal = document.querySelector('#cconsent-modal');
            var feedback = document.querySelector(""[class^='feedback_inlineWrapper']"");
            var navContainer = document.querySelector(""[data-testid='page.desktopTableOfContents']"");
            if (navContainer) {
                navContainer.parentNode.removeChild(navContainer);
            }if (header) {
                header.parentNode.removeChild(header);
            }
            if (pheader) {
                pheader.parentNode.removeChild(pheader);
            }
            if (aside) {
                aside.parentNode.removeChild(aside);
            } if (docsNav) {
                docsNav.parentNode.removeChild(docsNav);
            }
            if (footer) {
                footer.parentNode.removeChild(footer);
            }
            if (cconsentBar) {
                cconsentBar.parentNode.removeChild(cconsentBar);
            }
            if (cconsentModal) {
                cconsentModal.parentNode.removeChild(cconsentModal);
            }
            if (feedback) {
                feedback.parentNode.removeChild(feedback);
            }

        }");
    }

    public static async Task SavePage(IPage page, int index, string pdfFolderPath)
    {
        // Set up PDF options
        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            MarginOptions = new MarginOptions
            {
                Top = "2cm",
                Right = "1cm",
                Bottom = "2cm",
                Left = "1cm"
            },
            DisplayHeaderFooter = false,
            PrintBackground = true
        };

        // Save PDF to the created folder
        var pdfFilePath = Path.Combine(pdfFolderPath, $"Article_{index}.pdf");
        // Save content to PDF
        await page.PdfAsync(pdfFilePath, pdfOptions);

        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var sortedPdfFiles = pdfFiles.OrderBy(f => GetFileNumber(f)).ToList();
        var finalFile = Path.Combine(pdfFolderPath, "openai-docs.pdf");
        MergePdfFiles(sortedPdfFiles, finalFile);
    }

    public static async Task SaveContentToPDF(List<PageContent> pagesContentList, string filePath)
    {
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions { ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe", Headless = true });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<html><body></body></html>"); // Set an initial empty HTML content

        foreach (var pageContent in pagesContentList)
        {
            // For each content, create a new section in the HTML
            string sectionHtml = $"<section><h1>{pageContent.Title}</h1><div>{pageContent.Content}</div></section>";
            await page.EvaluateFunctionAsync(@"(html) => {
            document.body.insertAdjacentHTML('beforeend', html);
        }", sectionHtml);

        }

        // Adjust PDF options as necessary
        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = { Top = "20px", Right = "20px", Bottom = "20px", Left = "20px" }
        };


        Console.WriteLine($"Saving PDF Document, path: {filePath}");
        await page.PdfAsync(filePath, pdfOptions);

        await browser.CloseAsync();
    }

    private static string PrepareDirectory()
    {
        // Create a folder for saving PDFs
        var pdfFolderPath = Path.Combine(Environment.CurrentDirectory, "PDFs");

        if (Directory.Exists(pdfFolderPath))
        {
            // clear the folder
            Directory.Delete(pdfFolderPath, true);
            Directory.CreateDirectory(pdfFolderPath);
        }
        else
        {
            Directory.CreateDirectory(pdfFolderPath);
        }

        return pdfFolderPath;
    }

    static void saveAll(string pdfFolderPath)
    {
        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var sortedPdfFiles = pdfFiles.OrderBy(f => GetFileNumber(f)).ToList();
        // 打印排序后的文件名
        sortedPdfFiles.ForEach(f => Console.WriteLine(f));
        var finalFile = Path.Combine(pdfFolderPath, "nextjs-learn.pdf");
        MergePdfFiles(sortedPdfFiles, finalFile);
    }

    // 提取文件名中的数字并转换为整数
    static int GetFileNumber(string fileName)
    {
        // 正则表达式匹配所有数字
        var match = Regex.Match(Path.GetFileName(fileName), @"\d+");
        var num = match.Success ? int.Parse(match.Value) : 0;
        Console.WriteLine(num);
        return num;
    }

    static void MergePdfFiles(IEnumerable<string> inputPdfFiles, string outputPdfFile)
    {
        using (var writer = new PdfWriter(outputPdfFile))
        {
            using (var pdf = new PdfDocument(writer))
            {
                var pdfMerger = new PdfMerger(pdf);

                foreach (var inputFile in inputPdfFiles)
                {
                    using (var pdfToMerge = new PdfDocument(new PdfReader(inputFile)))
                    {
                        pdfMerger.Merge(pdfToMerge, 1, pdfToMerge.GetNumberOfPages());
                    }
                }
            }
        }
    }

}
