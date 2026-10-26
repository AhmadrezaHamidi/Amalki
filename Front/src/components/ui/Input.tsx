import { forwardRef, type InputHTMLAttributes, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";
import { clsx } from "@/lib/clsx";

// react-hook-form's formState.errors.field is a FieldError, not a string — accept both
// so callers can pass `formState.errors.x?.message` directly without casting.
type ErrorLike = string | { message?: string } | undefined;

interface FieldWrapperProps {
  label?: string;
  error?: ErrorLike;
}

function errorText(error: ErrorLike): string | undefined {
  if (!error) return undefined;
  return typeof error === "string" ? error : error.message;
}

interface InputProps extends InputHTMLAttributes<HTMLInputElement>, FieldWrapperProps {}

export const Input = forwardRef<HTMLInputElement, InputProps>(({ label, error, className, ...rest }, ref) => {
  const message = errorText(error);
  return (
    <label className="block">
      {label && <span className="mb-1 block text-sm font-medium text-gray-700">{label}</span>}
      <input
        ref={ref}
        className={clsx(
          "w-full rounded-lg border px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-500",
          message ? "border-red-400" : "border-brand-200",
          className
        )}
        {...rest}
      />
      {message && <span className="mt-1 block text-xs text-red-600">{message}</span>}
    </label>
  );
});
Input.displayName = "Input";

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement>, FieldWrapperProps {
  options: { value: string; label: string }[];
  placeholder?: string;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ label, error, options, placeholder, className, ...rest }, ref) => {
    const message = errorText(error);
    return (
      <label className="block">
        {label && <span className="mb-1 block text-sm font-medium text-gray-700">{label}</span>}
        <select
          ref={ref}
          className={clsx(
            "w-full rounded-lg border bg-white px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-500",
            message ? "border-red-400" : "border-brand-200",
            className
          )}
          {...rest}
        >
          {placeholder && <option value="">{placeholder}</option>}
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        {message && <span className="mt-1 block text-xs text-red-600">{message}</span>}
      </label>
    );
  }
);
Select.displayName = "Select";

interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement>, FieldWrapperProps {}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(({ label, error, className, ...rest }, ref) => {
  const message = errorText(error);
  return (
    <label className="block">
      {label && <span className="mb-1 block text-sm font-medium text-gray-700">{label}</span>}
      <textarea
        ref={ref}
        className={clsx(
          "w-full rounded-lg border px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-500",
          message ? "border-red-400" : "border-brand-200",
          className
        )}
        {...rest}
      />
      {message && <span className="mt-1 block text-xs text-red-600">{message}</span>}
    </label>
  );
});
Textarea.displayName = "Textarea";
