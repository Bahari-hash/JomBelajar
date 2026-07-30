import { UserRound } from "lucide-react";
import { Button } from "@/components/ui/button.jsx";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu.jsx";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip.jsx";

/** Account entry point kept intentionally inert until authentication is implemented. */
export function AccountMenu() {
  return (
    <DropdownMenu>
      <Tooltip>
        <TooltipTrigger asChild>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="账户菜单">
              <UserRound aria-hidden="true" />
            </Button>
          </DropdownMenuTrigger>
        </TooltipTrigger>
        <TooltipContent>账户菜单</TooltipContent>
      </Tooltip>
      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuLabel>账户</DropdownMenuLabel>
        <DropdownMenuItem disabled>账户信息不可用</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
