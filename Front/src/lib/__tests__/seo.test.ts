import { describe, expect, it } from "vitest";
import { serializeJsonLd } from "@/lib/seo";

describe("serializeJsonLd", () => {
  it("cannot close the script tag", () => {
    const json = serializeJsonLd({ name: "x</script><script>alert(1)</script>" });
    expect(json).not.toContain("</script");
    expect(JSON.parse(json).name).toBe("x</script><script>alert(1)</script>");
  });
});
