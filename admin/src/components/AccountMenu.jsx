import { useState } from "react";
import { LogOut, UserRound } from "lucide-react";
import { useDispatch, useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
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
import { authSession } from "@/services/authSession.js";
import { clearApiSession } from "@/services/baseApi.js";
import { getRoleLabel } from "@/services/roles.js";

/** Shows the verified administrator identity and provides reliable local logout. */
export function AccountMenu() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const user = useSelector((state) => state.auth.user);
  const [loggingOut, setLoggingOut] = useState(false);

  const handleLogout = async (event) => {
    event.preventDefault();
    if (loggingOut) return;
    setLoggingOut(true);
    try {
      await authSession.logout();
    } finally {
      clearApiSession(dispatch);
      navigate("/login", { replace: true });
    }
  };

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
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel className="min-w-0">
          <span className="block truncate" title={user?.email}>
            {user?.email}
          </span>
          <span className="mt-0.5 block text-xs font-normal text-muted-foreground">
            {getRoleLabel(user?.role)}
          </span>
        </DropdownMenuLabel>
        <DropdownMenuItem disabled={loggingOut} onSelect={handleLogout}>
          <LogOut aria-hidden="true" />
          {loggingOut ? "正在退出" : "退出登录"}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
