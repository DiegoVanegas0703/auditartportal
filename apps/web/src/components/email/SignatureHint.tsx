import { Link } from 'react-router-dom'

export function SignatureHint({ hasSignature }: { hasSignature: boolean }) {
  return (
    <p className="mt-1 text-[11px] text-auditart-muted">
      {hasSignature ? 'Tu firma se agregó al final. ' : 'Todavía no configuraste una firma. '}
      <Link to="/mi-cuenta" className="font-semibold text-auditart-blue hover:underline">
        {hasSignature ? 'Editar firma' : 'Configurar firma'}
      </Link>
    </p>
  )
}
