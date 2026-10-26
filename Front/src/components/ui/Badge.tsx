import { clsx } from "@/lib/clsx";
import type { BadgeColor } from "@/types/enums";

const colorMap: Record<BadgeColor, string> = {
  gray: "bg-gray-100 text-gray-700",
  blue: "bg-blue-100 text-blue-700",
  amber: "bg-amber-100 text-amber-700",
  green: "bg-green-100 text-green-700",
  red: "bg-red-100 text-red-700",
  brand: "bg-brand-100 text-brand-800",
};

export function Badge({ color = "gray", children }: { color?: BadgeColor; children: React.ReactNode }) {
  return (
    <span className={clsx("inline-block rounded-full px-2.5 py-1 text-xs font-medium", colorMap[color])}>
      {children}
    </span>
  );
}
