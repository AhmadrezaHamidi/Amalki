import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button, Input, Textarea, ErrorState } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { useSubmitLead } from "@/features/leads/api";

const schema = z.object({
  fullName: z.string().trim().min(2, "نام را وارد کنید").max(100, "نام حداکثر ۱۰۰ نویسه است"),
  phoneNumber: z.string().regex(/^09\d{9}$/, "شماره موبایل معتبر نیست (مثال: 09123456789)"),
  message: z.string().max(1000, "پیام حداکثر ۱۰۰۰ نویسه است").optional(),
});

type Values = z.infer<typeof schema>;

/** فرم «درخواست تماس» — بدون ورود؛ مدیر پیامک می‌گیرد */
export function LeadForm({ propertyId }: { propertyId?: number }) {
  const submit = useSubmitLead();
  const form = useForm<Values>({ resolver: zodResolver(schema) });

  if (submit.isSuccess)
    return (
      <div role="status" className="rounded-xl bg-brand-100 p-4 text-sm text-brand-900">
        درخواست شما ثبت شد. مشاور به‌زودی با شما تماس می‌گیرد.
      </div>
    );

  return (
    <form
      id="contact"
      aria-label="درخواست تماس"
      className="space-y-3"
      onSubmit={form.handleSubmit((v) => submit.mutate({ ...v, propertyId, message: v.message || undefined }))}
    >
      <Input label="نام و نام خانوادگی" autoComplete="name" {...form.register("fullName")} error={form.formState.errors.fullName} />
      <Input
        label="شماره موبایل"
        dir="ltr"
        inputMode="tel"
        autoComplete="tel"
        placeholder="09123456789"
        {...form.register("phoneNumber")}
        error={form.formState.errors.phoneNumber}
      />
      <Textarea label="پیام (اختیاری)" rows={3} {...form.register("message")} error={form.formState.errors.message} />
      {submit.isError && <ErrorState message={extractErrorMessage(submit.error)} />}
      <Button type="submit" className="w-full" loading={submit.isPending}>
        درخواست تماس
      </Button>
    </form>
  );
}
