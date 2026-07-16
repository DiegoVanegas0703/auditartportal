import type { Permission, User, UserRole } from '../types'

export const MOCK_USERS: User[] = [
  {
    id: 'u1',
    name: 'Diego Santamarina',
    email: 'diego.santamarina@auditart.com.ar',
    role: 'admin',
  },
  {
    id: 'u2',
    name: 'María González',
    email: 'maria.gonzalez@auditart.com.ar',
    role: 'jefatura',
  },
  {
    id: 'u3',
    name: 'Pablo Rodríguez',
    email: 'pablo.rodriguez@auditart.com.ar',
    role: 'operador',
    queue: 'general',
  },
  {
    id: 'u4',
    name: 'Laura Fernández',
    email: 'laura.fernandez@auditart.com.ar',
    role: 'operador',
    queue: 'general',
  },
  {
    id: 'u5',
    name: 'Martín Acosta',
    email: 'martin.acosta@auditart.com.ar',
    role: 'operador',
    queue: 'general',
  },
  {
    id: 'u6',
    name: 'Aylen Martínez',
    email: 'aylen.martinez@auditart.com.ar',
    role: 'telemedicina',
    queue: 'telemedicina',
  },
  {
    id: 'u7',
    name: 'Carolina Ruiz',
    email: 'cronicos@auditart.com.ar',
    role: 'cronicos',
    queue: 'cronicos',
  },
  {
    id: 'u8',
    name: 'Damián López',
    email: 'damian.lopez@auditart.com.ar',
    role: 'facturacion',
  },
]

export const DEFAULT_USER = MOCK_USERS[0]

export function getPermissions(role: UserRole): Permission {
  switch (role) {
    case 'admin':
      return {
        triage: true,
        operationalBoard: true,
        billing: true,
        allQueues: true,
        manageUsers: true,
      }
    case 'jefatura':
      return {
        triage: true,
        operationalBoard: true,
        billing: false,
        allQueues: true,
        manageUsers: false,
      }
    case 'operador':
      return {
        triage: false,
        operationalBoard: true,
        billing: false,
        allQueues: false,
        manageUsers: false,
      }
    case 'telemedicina':
      return {
        triage: false,
        operationalBoard: true,
        billing: false,
        allQueues: false,
        manageUsers: false,
      }
    case 'cronicos':
      return {
        triage: false,
        operationalBoard: true,
        billing: false,
        allQueues: false,
        manageUsers: false,
      }
    case 'facturacion':
      return {
        triage: false,
        operationalBoard: false,
        billing: true,
        allQueues: false,
        manageUsers: false,
      }
  }
}
