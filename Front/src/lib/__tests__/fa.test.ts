import { describe, expect, it } from "vitest";
import { faDigits, faMoney, parseNumber } from "@/lib/fa";

describe("fa", () => {
  it("formats toman like the API caption builder", () => {
    expect(faMoney(5_000_000_000)).toBe("۵ میلیارد");
    expect(faMoney(12_500_000_000)).toBe("۱۲٫۵ میلیارد");
    expect(faMoney(400_000_000)).toBe("۴۰۰ میلیون");
    expect(faMoney(950_000)).toBe("۹۵۰,۰۰۰");
    expect(faMoney(null)).toBe("توافقی");
  });

  it("converts digits", () => expect(faDigits("90 متر")).toBe("۹۰ متر"));

  it("parses persian digits and separators", () => {
    expect(parseNumber("۵٬۰۰۰")).toBe(5_000);
    expect(parseNumber("۵,۰۰۰,۰۰۰")).toBe(5_000_000);
    expect(parseNumber("12 000")).toBe(12_000);
    expect(parseNumber("")).toBeUndefined();
    expect(parseNumber("abc")).toBeUndefined();
  });
});
