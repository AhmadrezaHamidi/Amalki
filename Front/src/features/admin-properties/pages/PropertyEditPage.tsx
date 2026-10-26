import { useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Badge, Button, Card, CardTitle, ErrorState, Input, Select, Spinner, Textarea } from "@/components/ui";
import { extractErrorMessage } from "@/lib/apiClient";
import { dealLabels, kindLabels, options, statusColor, statusLabels } from "@/types/enums";
import {
  useAdminProperty,
  useCreateProperty,
  usePropertyAction,
  useUpdateProperty,
} from "@/features/admin-properties/api";
import { emptyProperty, propertySchema, toFormValues, toInput, type PropertyFormValues } from "@/features/admin-properties/schema";
import { PhotoManager } from "@/features/admin-properties/components/PhotoManager";
import { ChannelPanel } from "@/features/admin-properties/components/ChannelPanel";
import { propertyPath } from "@/features/listing/paths";

export function PropertyEditPage() {
  const params = useParams();
  const id = Number(params.id ?? 0);
  const isNew = !params.id;
  const navigate = useNavigate();

  const property = useAdminProperty(id);
  const create = useCreateProperty();
  const update = useUpdateProperty(id);
  const action = usePropertyAction();

  const form = useForm<PropertyFormValues>({ resolver: zodResolver(propertySchema), defaultValues: emptyProperty });
  const deal = form.watch("deal");
  const errors = form.formState.errors;

  useEffect(() => {
    if (property.data) form.reset(toFormValues(property.data));
  }, [property.data, form]);

  const save = form.handleSubmit(async (values) => {
    const input = toInput(propertySchema.parse(values));
    if (isNew) {
      const created = await create.mutateAsync(input);
      // پس از ساخت به صفحهٔ ویرایش می‌رود تا عکس اضافه و منتشر شود
      navigate(`/admin/properties/${created.id}`, { replace: true });
    } else {
      await update.mutateAsync(input);
    }
  });

  if (!isNew && property.isLoading) return <Spinner />;
  if (!isNew && property.isError) return <ErrorState message={extractErrorMessage(property.error)} />;
  if (!isNew && property.data === null) return <ErrorState message="آگهی یافت نشد." />;

  const p = property.data;
  const saveError = create.error ?? update.error;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center gap-3">
        <Link to="/admin/properties" className="text-sm text-brand-700">← آگهی‌ها</Link>
        <h1 className="text-2xl font-bold">{isNew ? "آگهی جدید" : p?.title}</h1>
        {p && <Badge color={statusColor[p.status]}>{statusLabels[p.status]}</Badge>}
        {p && (
          <div className="ms-auto flex flex-wrap gap-2">
            {p.status !== "Published" ? (
              <Button onClick={() => action.mutate({ id, action: "Publish" })} loading={action.isPending}>انتشار در سایت و کانال‌ها</Button>
            ) : (
              <>
                <a href={propertyPath(p.slug)} target="_blank" rel="noreferrer" className="px-3 py-2 text-sm text-brand-700">مشاهده در سایت ↗</a>
                <Button variant="secondary" onClick={() => action.mutate({ id, action: "Unpublish" })} loading={action.isPending}>لغو انتشار</Button>
              </>
            )}
          </div>
        )}
      </div>
      {action.isError && <ErrorState message={extractErrorMessage(action.error)} />}

      <div className="grid gap-5 lg:grid-cols-[2fr_1fr]">
        <Card>
          <CardTitle>مشخصات</CardTitle>
          <form onSubmit={save} className="space-y-4" noValidate>
            <Input label="عنوان" {...form.register("title")} error={errors.title} />
            <div className="grid gap-3 sm:grid-cols-2">
              <Select label="نوع معامله" options={options(dealLabels)} {...form.register("deal")} />
              <Select label="نوع ملک" options={options(kindLabels)} {...form.register("kind")} />
            </div>
            {deal === "Sale" ? (
              <Input label="قیمت (تومان)" inputMode="numeric" {...form.register("price")} error={errors.price} />
            ) : (
              <div className="grid gap-3 sm:grid-cols-2">
                <Input label="رهن (تومان)" inputMode="numeric" {...form.register("deposit")} error={errors.deposit} />
                <Input label="اجارهٔ ماهانه (تومان)" inputMode="numeric" {...form.register("monthlyRent")} error={errors.monthlyRent} />
              </div>
            )}
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
              <Input label="متراژ" inputMode="numeric" {...form.register("areaM2")} error={errors.areaM2} />
              <Input label="خواب" inputMode="numeric" {...form.register("bedrooms")} error={errors.bedrooms} />
              <Input label="طبقه" inputMode="numeric" {...form.register("floor")} error={errors.floor} />
              <Input label="پارکینگ" inputMode="numeric" {...form.register("parking")} error={errors.parking} />
              <Input label="سال ساخت" inputMode="numeric" placeholder="۱۴۰۰" {...form.register("yearBuilt")} error={errors.yearBuilt} />
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Input label="شهر" {...form.register("city")} error={errors.city} />
              <Input label="محله" {...form.register("district")} error={errors.district} />
            </div>
            <Input label="آدرس دقیق (در سایت نمایش داده نمی‌شود)" {...form.register("address")} error={errors.address} />
            <div className="grid gap-3 sm:grid-cols-2">
              <Input label="عرض جغرافیایی" dir="ltr" inputMode="decimal" placeholder="35.7" {...form.register("latitude")} error={errors.latitude} />
              <Input label="طول جغرافیایی" dir="ltr" inputMode="decimal" placeholder="51.4" {...form.register("longitude")} error={errors.longitude} />
            </div>
            <Textarea label="توضیحات" rows={6} {...form.register("description")} error={errors.description} />
            {saveError && <ErrorState message={extractErrorMessage(saveError)} />}
            {update.isSuccess && !form.formState.isDirty && <p role="status" className="text-sm text-brand-700">ذخیره شد.</p>}
            <Button type="submit" loading={form.formState.isSubmitting}>
              {isNew ? "ثبت پیش‌نویس" : "ذخیرهٔ تغییرات"}
            </Button>
          </form>
        </Card>

        {p ? (
          <div className="space-y-5">
            <ChannelPanel propertyId={id} />
          </div>
        ) : (
          <Card>
            <p className="text-sm text-slate-600">پس از ثبت پیش‌نویس می‌توانید عکس اضافه و آگهی را منتشر کنید.</p>
          </Card>
        )}
      </div>

      {p && <PhotoManager propertyId={id} photos={p.photos} />}
    </div>
  );
}
