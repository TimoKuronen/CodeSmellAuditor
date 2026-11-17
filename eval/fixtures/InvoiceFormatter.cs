using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Billing.Formatting
{
    /// <summary>
    /// Renders an invoice as plain text for archiving.
    /// </summary>
    public sealed class InvoiceFormatter
    {
        private readonly CultureInfo culture;
        private readonly int lineWidth;

        public InvoiceFormatter(CultureInfo culture, int lineWidth)
        {
            if (lineWidth < 20)
            {
                throw new ArgumentOutOfRangeException(nameof(lineWidth), "Line width must be at least 20.");
            }

            this.culture = culture ?? throw new ArgumentNullException(nameof(culture));
            this.lineWidth = lineWidth;
        }

        public string Format(Invoice invoice)
        {
            if (invoice == null)
            {
                throw new ArgumentNullException(nameof(invoice));
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Invoice {invoice.Number}");
            builder.AppendLine(new string('-', lineWidth));

            foreach (InvoiceLine line in invoice.Lines)
            {
                builder.AppendLine(FormatLine(line));
            }

            builder.AppendLine(new string('-', lineWidth));
            builder.AppendLine(FormatTotal(invoice.Total));
            return builder.ToString();
        }

        private string FormatLine(InvoiceLine line)
        {
            string amount = line.Amount.ToString("C", culture);
            int padding = Math.Max(1, lineWidth - line.Description.Length - amount.Length);
            return line.Description + new string(' ', padding) + amount;
        }

        private string FormatTotal(decimal total)
        {
            string amount = total.ToString("C", culture);
            return "Total".PadRight(lineWidth - amount.Length) + amount;
        }
    }
}
