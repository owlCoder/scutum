import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { MODULES, ModuleOverview } from "./module-overview";

describe("ModuleOverview", () => {
  it("prikazuje sve tri celine T10 sa oznakom celine", () => {
    render(<ModuleOverview />);

    const section = screen.getByRole("region", { name: "Moduli" });
    for (const item of MODULES) {
      expect(within(section).getByText(item.title)).toBeInTheDocument();
      expect(within(section).getByText(item.unit)).toBeInTheDocument();
    }
    expect(MODULES.map((m) => m.unit)).toEqual(["R1-19", "R2-17", "R3-15"]);
  });
});
