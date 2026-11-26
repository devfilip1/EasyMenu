import { Role } from '../../api/generated/models/role';

/** Where a user lands after signing in. */
export function homeFor(role: Role): string {
  return role === 'kitchen' ? '/cozinha' : '/admin/cardapio';
}
