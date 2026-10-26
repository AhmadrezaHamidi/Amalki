import type { HTMLAttributes } from "react";
import { clsx } from "@/lib/clsx";

export function Card({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={clsx("rounded-2xl border border-brand-200 bg-white p-5 shadow-sm", className)} {...rest} />;
}

export function CardTitle({ className, ...rest }: HTMLAttributes<HTMLHeadingElement>) {
  return <h2 className={clsx("mb-4 text-base font-semibold text-gray-900", className)} {...rest} />;
}
