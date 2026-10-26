import { describe, expect, it } from "vitest";
import { fromSearchParams, toQueryString } from "@/features/listing/filterParams";

describe("filterParams", () => {
  it("round-trips a filter through the URL", () => {
    const filter = { deal: "Rent" as const, city: "تهران", minArea: 80, sort: "PriceAsc" as const };
    expect(fromSearchParams(new URLSearchParams(toQueryString(filter).slice(1)))).toEqual(filter);
  });

  it("drops unknown enum values and junk numbers instead of sending them to the API", () => {
    expect(fromSearchParams(new URLSearchParams("deal=Buy&kind=Castle&maxPrice=-5&page=abc&city=%20"))).toEqual({});
  });

  it("omits empty values from the query string", () => {
    expect(toQueryString({ deal: undefined, city: "" })).toBe("");
  });
});
