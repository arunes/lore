import { HugeiconsIcon, type IconSvgElement } from "@hugeicons/react"
import { cn } from "@/lib/utils"

export function Icon({
  icon,
  size = 18,
  className,
}: {
  icon: IconSvgElement
  size?: number
  className?: string
}) {
  return (
    <HugeiconsIcon
      icon={icon}
      size={size}
      className={cn("shrink-0", className)}
    />
  )
}
