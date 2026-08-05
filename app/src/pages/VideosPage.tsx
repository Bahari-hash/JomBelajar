import { Clapperboard } from "lucide-react";
import ModulePlaceholder from "@/components/ModulePlaceholder";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function VideosPage() {
  useDocumentTitle("视频");
  return (
    <ModulePlaceholder
      title="视频"
      description="观看外语视频，在真实语境中训练听力与理解能力。"
      icon={Clapperboard}
    />
  );
}
