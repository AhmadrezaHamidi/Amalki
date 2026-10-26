/** ارقام لاتین → فارسی */
export function faDigits(value: string | number): string {
  return String(value).replace(/\d/g, (d) => "۰۱۲۳۴۵۶۷۸۹"[Number(d)]);
}

/** مبلغ تومانی به‌صورت «۱۲٫۵ میلیارد» / «۴۰۰ میلیون»؛ خالی یعنی «توافقی» — هم‌رفتار با Fa.Money در API */
export function faMoney(toman: number | null | undefined): string {
  if (!toman) return "توافقی";
  const trim = (n: number) => (Math.round(n * 100) / 100).toString().replace(".", "٫");
  if (toman >= 1_000_000_000) return faDigits(`${trim(toman / 1_000_000_000)} میلیارد`);
  if (toman >= 1_000_000) return faDigits(`${trim(toman / 1_000_000)} میلیون`);
  return faDigits(toman.toLocaleString("en-US"));
}

/** تاریخ به تقویم شمسی */
export function faDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  return new Intl.DateTimeFormat("fa-IR", { dateStyle: "medium" }).format(new Date(iso));
}

/** ورودی عددی که ممکن است ارقام فارسی داشته باشد → عدد یا undefined */
export function parseNumber(input: string | number | null | undefined): number | undefined {
  if (input === null || input === undefined || input === "") return undefined;
  if (typeof input === "number") return Number.isFinite(input) ? input : undefined;
  const latin = input.replace(/[۰-۹]/g, (d) => String("۰۱۲۳۴۵۶۷۸۹".indexOf(d))).replace(/[,،٬\s]/g, "");
  if (latin === "") return undefined;
  const n = Number(latin);
  return Number.isFinite(n) ? n : undefined;
}
