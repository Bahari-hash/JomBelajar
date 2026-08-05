import { Eye, EyeOff } from "lucide-react";
import { useState } from "react";

interface PasswordFieldProps {
  id: string;
  label: string;
  value: string;
  error?: string;
  autoComplete: string;
  onChange: (value: string) => void;
}

/** Password input with an accessible visibility toggle and inline validation. */
export default function PasswordField({
  id,
  label,
  value,
  error,
  autoComplete,
  onChange,
}: PasswordFieldProps) {
  const isConfirmation = id.endsWith("confirmation");
  const [visible, setVisible] = useState(false);

  return (
    <label className="form-control w-full" htmlFor={id}>
      <span className="label pb-1">
        <span className="label-text font-medium">{label}</span>
      </span>
      <span className="relative">
        <input
          id={id}
          aria-describedby={error ? `${id}-error` : undefined}
          aria-invalid={Boolean(error)}
          autoComplete={autoComplete}
          className="input input-bordered w-full pr-11"
          maxLength={50}
          type={visible && !isConfirmation ? "text" : "password"}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
        {isConfirmation ? null : (
          <button
            aria-label="显示或隐藏密码"
            className="btn btn-square btn-ghost btn-sm absolute right-1 top-1/2 -translate-y-1/2"
            type="button"
            onClick={() => setVisible((current) => !current)}
          >
            {visible ? (
              <EyeOff aria-hidden="true" className="size-4" />
            ) : (
              <Eye aria-hidden="true" className="size-4" />
            )}
          </button>
        )}
      </span>
      {error ? (
        <span className="label pt-1 text-error" id={`${id}-error`}>
          {error}
        </span>
      ) : null}
    </label>
  );
}
