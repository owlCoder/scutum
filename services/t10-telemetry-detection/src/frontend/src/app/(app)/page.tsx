import { ModuleOverview } from "@/components/layout/module-overview";

export default function HomePage() {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="font-heading text-2xl font-semibold">Pregled</h1>
        <p className="text-sm text-muted-foreground">
          Freshness i coverage izvora pojaviće se ovde kada registar izvora (R1-19) bude spreman.
        </p>
      </div>
      <ModuleOverview />
    </div>
  );
}
