namespace WebsiteHealthMonitor.Services
{
    public class WebsiteCatalog
    {
        public static readonly IReadOnlyList<(int Id, string Name, string Url)> All =
            new List<(int Id, string Name, string Url)>
            {
                (1, "Khajane",           "https://khajane.karnataka.gov.in/en"),
                (2, "e-HRMS",            "https://e-hrms.gov.in/home"),
                (3, "HRMS 2",            "https://hrms2.karnataka.gov.in/v1/login"),
                (4, "Not Working Test Website", "https://httpbin.org/status/500"),
                (5, "Mahiti Kanaja",     "https://mahitikanaja.karnataka.gov.in/")
            };
    }
}
