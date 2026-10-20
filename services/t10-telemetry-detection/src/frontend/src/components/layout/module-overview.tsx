import { Badge } from "@/components/ui/badge";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export type ModuleInfo = {
  unit: string;
  title: string;
  description: string;
  milestone: string;
};

// Tri modula servisa T10; svaki dobija svoje stranice u app/(app)/<modul> i kod u features/<modul>.
export const MODULES: readonly ModuleInfo[] = [
  {
    unit: "R1-19",
    title: "Registar izvora",
    description: "Security i log izvori, vlasnici, šeme, collection status, freshness i coverage.",
    milestone: "KT2",
  },
  {
    unit: "R2-17",
    title: "Telemetrija",
    description: "Prijem, validacija, normalizacija i deduplikacija security događaja.",
    milestone: "KT3",
  },
  {
    unit: "R3-15",
    title: "Detection engineering",
    description: "Verzije pravila, test corpus, simulacija, metrike kvaliteta i promocija.",
    milestone: "KT3/KT4",
  },
];

export function ModuleOverview({ modules = MODULES }: { modules?: readonly ModuleInfo[] }) {
  return (
    <section aria-label="Moduli" className="grid gap-4 md:grid-cols-3">
      {modules.map((item) => (
        <Card key={item.unit}>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle>{item.title}</CardTitle>
              <Badge variant="outline">{item.unit}</Badge>
            </div>
            <CardDescription>{item.description}</CardDescription>
            <p className="text-xs text-muted-foreground">U izradi · {item.milestone}</p>
          </CardHeader>
        </Card>
      ))}
    </section>
  );
}
