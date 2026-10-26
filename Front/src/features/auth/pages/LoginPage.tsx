import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate } from "react-router-dom";
import { useSeo } from "@/lib/seo";
import { Button, Card, ErrorState } from "@/components/ui";
import { Input } from "@/components/ui/Input";
import { useSendOtp, useVerifyOtp } from "@/features/auth/api";
import { useAuthStore } from "@/store/authStore";
import { extractErrorMessage } from "@/lib/apiClient";

const phoneSchema = z.object({
  phoneNumber: z.string().regex(/^09\d{9}$/, "شماره موبایل معتبر نیست (مثال: 09123456789)"),
});
const codeSchema = z.object({
  code: z.string().regex(/^\d{4,6}$/, "کد تایید باید ۴ تا ۶ رقم باشد"),
});

export function LoginPage() {
  const [phase, setPhase] = useState<"phone" | "code">("phone");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [devHint, setDevHint] = useState<string | null>(null);
  const setSession = useAuthStore((s) => s.setSession);
  const navigate = useNavigate();

  useSeo({ title: "ورود مدیر", robots: "noindex,nofollow" });

  const sendOtp = useSendOtp();
  const verifyOtp = useVerifyOtp();

  const phoneForm = useForm<z.infer<typeof phoneSchema>>({ resolver: zodResolver(phoneSchema) });
  const codeForm = useForm<z.infer<typeof codeSchema>>({ resolver: zodResolver(codeSchema) });

  async function onSendOtp(values: { phoneNumber: string }) {
    setDevHint(null);
    try {
      const message = await sendOtp.mutateAsync(values.phoneNumber);
      setPhoneNumber(values.phoneNumber);
      if (message.startsWith("SMS_UNAVAILABLE:")) {
        setDevHint(`کد تست (محیط توسعه): ${message.split(":")[1]}`);
      }
      setPhase("code");
    } catch {
      // error shown via sendOtp.error
    }
  }

  async function onVerifyOtp(values: { code: string }) {
    try {
      const res = await verifyOtp.mutateAsync({ phoneNumber, code: values.code });
      setSession(res);
      navigate(res.role === "Admin" ? "/admin" : "/");
    } catch {
      // error shown via verifyOtp.error
    }
  }

  return (
    <Card>
      <h2 className="mb-4 text-lg font-bold">ورود به پنل مدیریت</h2>
      {phase === "phone" ? (
        <form onSubmit={phoneForm.handleSubmit(onSendOtp)} className="space-y-4">
          <Input
            label="شماره موبایل"
            placeholder="09123456789"
            dir="ltr"
            {...phoneForm.register("phoneNumber")}
            error={phoneForm.formState.errors.phoneNumber?.message as string}
          />
          {sendOtp.isError && <ErrorState message={extractErrorMessage(sendOtp.error)} />}
          <Button type="submit" className="w-full" loading={sendOtp.isPending}>
            دریافت کد تایید
          </Button>
        </form>
      ) : (
        <form onSubmit={codeForm.handleSubmit(onVerifyOtp)} className="space-y-4">
          <p className="text-sm text-gray-500">کد ارسال‌شده به {phoneNumber} را وارد کنید</p>
          {devHint && <p className="text-xs text-amber-600">{devHint}</p>}
          <Input
            label="کد تایید"
            dir="ltr"
            inputMode="numeric"
            {...codeForm.register("code")}
            error={codeForm.formState.errors.code?.message as string}
          />
          {verifyOtp.isError && <ErrorState message={extractErrorMessage(verifyOtp.error)} />}
          <Button type="submit" className="w-full" loading={verifyOtp.isPending}>
            ورود
          </Button>
          <button type="button" className="w-full text-center text-sm text-gray-500" onClick={() => setPhase("phone")}>
            بازگشت
          </button>
        </form>
      )}
    </Card>
  );
}
