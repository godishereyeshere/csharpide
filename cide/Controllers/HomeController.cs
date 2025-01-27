using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Razor.Editor;

public class HomeController : Controller
{
    public ActionResult Index()
    {
        return View();
    }
    private async Task<string> CompileAndRun(string code)
    {
        // ایجاد درخت سینتکس (Syntax Tree) از کد
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        // ایجاد مرجع‌های لازم
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // ایجاد کامپایلر
        var compilation = CSharpCompilation.Create(
            "DynamicAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

        // کامپایل کد به صورت مموری استریم
        using (var ms = new MemoryStream())
        {
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                // اگر کامپایل موفق نبود، خطاها رو برگردون
                var errors = result.Diagnostics
                    .Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();
                return string.Join("\n", errors);
            }

            // اگر کامپایل موفق بود، کد رو اجرا کن
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            // پیدا کردن متد Main
            var entryPoint = assembly.EntryPoint;
            if (entryPoint != null)
            {
                using (var sw = new StringWriter())
                {
                    // ذخیره کردن خروجی پیش‌فرض کنسول
                    var originalConsoleOut = Console.Out;

                    try
                    {
                        // تغییر خروجی کنسول به StringWriter
                        Console.SetOut(sw);

                        // بررسی نوع پارامترهای متد Main
                        var parameters = entryPoint.GetParameters();
                        if (parameters.Length == 0)
                        {
                            // اگر Main بدون پارامتر بود
                            entryPoint.Invoke(null, null);
                        }
                        else
                        {
                            // اگر Main با پارامتر بود (مثل Main(string[] args))
                            entryPoint.Invoke(null, new object[] { Array.Empty<string>() });
                        }

                        // برگردوندن خروجی
                        return sw.ToString();
                    }
                    finally
                    {
                        // بازگردوندن خروجی کنسول به حالت پیش‌فرض
                        Console.SetOut(originalConsoleOut);
                    }
                }
            }
            else
            {
                return "متد Main پیدا نشد.";
            }
        }

    }

    [HttpPost]
    public async Task<ActionResult> RunCode(string code)
    {
        try
        {
            var result = await CompileAndRun(code); // کامپایل و اجرای کد
            return Content(result); // برگردوندن نتیجه به عنوان متن
        }
        catch (Exception ex)
        {
            return Content($"خطا: {ex.Message}"); // برگردوندن خطا به عنوان متن
        }
    }
}
