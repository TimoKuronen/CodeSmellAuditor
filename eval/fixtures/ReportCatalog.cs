using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Reporting.Catalog
{
    /// <summary>
    /// Builds the text reports shown on the management dashboard.
    /// </summary>
    public class ReportCatalog
    {
        public static Dictionary<string, string> RenderedCache = new Dictionary<string, string>();

        private readonly IClock clock;
        private readonly CultureInfo culture;
        private readonly ReportOptions options;

        public ReportCatalog(IClock clock, CultureInfo culture, ReportOptions options)
        {
            this.clock = clock;
            this.culture = culture;
            this.options = options;
        }

        public string BuildSalesReport(IReadOnlyList<SalesRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Sales report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<SalesRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (SalesRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 10)
            {
                builder.AppendLine("Large sales volume for the period.");
            }

            string text = builder.ToString();
            string key = "sales:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildRefundReport(IReadOnlyList<RefundRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Refund report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<RefundRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (RefundRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 20)
            {
                builder.AppendLine("Large refund volume for the period.");
            }

            string text = builder.ToString();
            string key = "refund:" + from.Ticks + ":" + to.Ticks;

            try
            {
                RenderedCache[key] = text;
            }
            catch (Exception)
            {
            }
            return text;
        }
        public string BuildShipmentReport(IReadOnlyList<ShipmentRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Shipment report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<ShipmentRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (ShipmentRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 30)
            {
                builder.AppendLine("Large shipment volume for the period.");
            }

            string text = builder.ToString();
            string key = "shipment:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildSupplierReport(IReadOnlyList<SupplierRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Supplier report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            // TODO: include archived suppliers once the policy is agreed
            IEnumerable<SupplierRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (SupplierRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 40)
            {
                builder.AppendLine("Large supplier volume for the period.");
            }

            string text = builder.ToString();
            string key = "supplier:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildPayrollReport(IReadOnlyList<PayrollRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Payroll report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<PayrollRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (PayrollRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 50)
            {
                builder.AppendLine("Large payroll volume for the period.");
            }

            string text = builder.ToString();
            string key = "payroll:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildExpenseReport(IReadOnlyList<ExpenseRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Expense report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<ExpenseRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (ExpenseRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 60)
            {
                builder.AppendLine("Large expense volume for the period.");
            }

            string text = builder.ToString();
            string key = "expense:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildTicketReport(IReadOnlyList<TicketRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Ticket report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<TicketRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (TicketRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 70)
            {
                builder.AppendLine("Large ticket volume for the period.");
            }

            string text = builder.ToString();
            string key = "ticket:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildCampaignReport(IReadOnlyList<CampaignRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Campaign report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<CampaignRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (CampaignRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 80)
            {
                builder.AppendLine("Large campaign volume for the period.");
            }

            string text = builder.ToString();
            string key = "campaign:" + from.Ticks + ":" + to.Ticks;

            return text;
        }
        public string BuildWarrantyReport(IReadOnlyList<WarrantyRecord> records, DateTime from, DateTime to)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Warranty report");
            builder.AppendLine("Generated " + clock.UtcNow.ToString("u", culture));
            builder.AppendLine(new string('=', options.Width));

            IEnumerable<WarrantyRecord> inRange = records
                .Where(record => record.Date >= from && record.Date <= to)
                .OrderBy(record => record.Date);

            decimal total = 0m;
            int count = 0;
            var perOwner = new Dictionary<string, decimal>();

            foreach (WarrantyRecord record in inRange)
            {
                total += record.Amount;
                count++;

                if (!perOwner.ContainsKey(record.Owner))
                {
                    perOwner[record.Owner] = 0m;
                }

                perOwner[record.Owner] += record.Amount;
                builder.AppendLine(record.Date.ToString("yyyy-MM-dd", culture) + "  " + record.Owner.PadRight(20) + record.Amount.ToString("N2", culture));
            }

            builder.AppendLine(new string('-', options.Width));
            builder.AppendLine("Records: " + count);
            builder.AppendLine("Total: " + total.ToString("N2", culture));

            foreach (KeyValuePair<string, decimal> pair in perOwner.OrderByDescending(p => p.Value).Take(options.TopOwners))
            {
                builder.AppendLine("  " + pair.Key.PadRight(20) + pair.Value.ToString("N2", culture));
            }

            if (count > 90)
            {
                builder.AppendLine("Large warranty volume for the period.");
            }

            string text = builder.ToString();
            string key = "warranty:" + from.Ticks + ":" + to.Ticks;

            return text;
        }    }
}
