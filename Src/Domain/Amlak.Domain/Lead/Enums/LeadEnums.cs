namespace Amlak.Domain.Lead.Enums;

/// <summary>وضعیت پیگیری درخواست تماس</summary>
public enum LeadStatus
{
    /// <summary>ثبت شده، هنوز پیگیری نشده</summary>
    New = 1,

    /// <summary>با مشتری تماس گرفته شد</summary>
    Contacted = 2,

    /// <summary>پیگیری تمام شد</summary>
    Closed = 3,

    /// <summary>هرزنامه</summary>
    Spam = 4
}
