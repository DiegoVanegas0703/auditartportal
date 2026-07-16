interface LogoProps {
  size?: 'sm' | 'md' | 'lg'
  showText?: boolean
  variant?: 'default' | 'light'
}

export function Logo({ size = 'md', showText = true, variant = 'default' }: LogoProps) {
  const sizes = { sm: 32, md: 42, lg: 60 }
  const s = sizes[size]
  const isLight = variant === 'light'

  return (
    <div className="flex items-center gap-3">
      <div
        className={`flex items-center justify-center rounded-xl ${
          isLight ? 'bg-white/10 p-1.5' : 'bg-auditart-blue/10 p-1.5'
        }`}
      >
        <svg width={s} height={s} viewBox="0 0 48 48" fill="none" aria-hidden>
          <rect x="8" y="18" width="32" height="12" rx="3" fill={isLight ? '#7eb3cc' : '#5D92B1'} />
          <rect x="18" y="8" width="12" height="32" rx="3" fill={isLight ? '#7eb3cc' : '#5D92B1'} />
          <circle cx="24" cy="24" r="7" fill={isLight ? '#ffffff' : '#2C3E50'} />
        </svg>
      </div>
      {showText && (
        <div>
          <span
            className={`font-[family-name:var(--font-display)] tracking-[0.15em] ${
              isLight
                ? 'text-white'
                : 'text-auditart-navy'
            } ${size === 'lg' ? 'text-3xl' : size === 'md' ? 'text-xl' : 'text-base'}`}
          >
            AUDITART
          </span>
          {size === 'lg' && (
            <p className={`text-sm ${isLight ? 'text-white/70' : 'text-auditart-gray'}`}>
              Portal de Gestión de Auditorías
            </p>
          )}
        </div>
      )}
    </div>
  )
}
