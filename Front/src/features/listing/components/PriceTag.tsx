import { faMoney } from "@/lib/fa";
import type { DealType } from "@/types/enums";

interface Props {
  deal: DealType;
  price: number | null;
  deposit: number | null;
  monthlyRent: number | null;
  large?: boolean;
}

export function PriceTag({ deal, price, deposit, monthlyRent, large }: Props) {
  const cls = large ? "text-xl font-extrabold text-brand-700" : "font-extrabold text-brand-700";
  if (deal === "Sale")
    return (
      <div className={cls}>
        {faMoney(price)} <small className="font-normal text-slate-500">تومان</small>
      </div>
    );
  return (
    <div className={cls}>
      رهن {faMoney(deposit)}
      {large ? <br /> : " "}
      <small className="font-normal text-slate-500">+ اجاره {faMoney(monthlyRent)} در ماه</small>
    </div>
  );
}
