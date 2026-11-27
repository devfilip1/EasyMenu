import { HttpErrorResponse } from '@angular/common/http';
import { ErrorCode } from '../../api/generated/models/error-code';
import { ERROR_CODE } from '../../api/generated/models/error-code-array';

/** Shared by the API's own size check (image_too_large) and the server limit (payload_too_large). */
const TOO_LARGE_MESSAGE = 'Arquivo grande demais (máx. 5 MB).';

const MESSAGES: Record<ErrorCode, string> = {
  validation_failed: 'Confira os campos destacados e tente novamente.',
  invalid_credentials: 'E-mail ou senha inválidos.',
  email_taken: 'Este e-mail já está cadastrado.',
  slug_taken: 'Este endereço já está em uso. Escolha outro.',
  invalid_slug: 'Endereço de cardápio inválido. Use letras minúsculas, números e hífens.',
  not_found: 'Não encontramos o que você procurava.',
  category_not_found: 'Categoria não encontrada.',
  category_not_empty: 'Remova ou mova os itens antes de excluir a categoria.',
  invalid_order_list:
    'A nova ordem não confere com a lista atual. Atualize a página e tente de novo.',
  invalid_price: 'Informe um preço maior que zero.',
  invalid_promotional_price: 'O preço promocional deve ser maior que zero e menor que o preço.',
  invalid_image_type: 'Formato de imagem não suportado. Envie JPG, PNG ou WebP.',
  image_too_large: TOO_LARGE_MESSAGE,
  establishment_not_found: 'Estabelecimento não encontrado.',
  empty_cart: 'Seu carrinho está vazio.',
  invalid_quantity: 'Quantidade inválida.',
  items_unavailable: 'Alguns itens do carrinho não estão mais disponíveis.',
  payment_declined: 'Pagamento recusado. Tente outro meio de pagamento.',
  order_not_found: 'Pedido não encontrado.',
  invalid_status_transition: 'Não é possível mudar o pedido para esse status agora.',
  order_not_ready: 'O pedido ainda não está pronto para retirada.',
  pickup_code_required: 'Informe o código de retirada',
  invalid_pickup_code: 'Código de retirada incorreto',
  order_already_picked_up: 'Este pedido já foi retirado',
  unauthorized: 'Sua sessão expirou. Entre novamente.',
  forbidden: 'Você não tem permissão para esta ação.',
  rate_limited: 'Muitas tentativas. Aguarde um instante e tente novamente.',
  payload_too_large: TOO_LARGE_MESSAGE,
};

const GENERIC_MESSAGE = 'Algo deu errado. Tente novamente em instantes.';

const KNOWN_CODES: ReadonlySet<string> = new Set(ERROR_CODE);

/** Extracts the stable `code` extension from a ProblemDetails error response, or `null`. */
export function problemCode(error: HttpErrorResponse): ErrorCode | null {
  const body = problemBody(error);
  if (body === null || !('code' in body)) return null;
  const { code } = body;
  return typeof code === 'string' && KNOWN_CODES.has(code) ? (code as ErrorCode) : null;
}

/**
 * The ProblemDetails body as an object. Operations without a JSON response (204) are requested
 * with `responseType: 'text'`, so their error bodies arrive as a JSON string.
 */
function problemBody(error: HttpErrorResponse): object | null {
  let body: unknown = error.error;
  if (typeof body === 'string') {
    try {
      body = JSON.parse(body);
    } catch {
      return null;
    }
  }
  return typeof body === 'object' && body !== null ? body : null;
}

/** pt-BR message for an `ErrorCode`; a generic message when the code is unknown or absent. */
export function problemMessage(code: ErrorCode | null | undefined): string {
  return (code && MESSAGES[code]) || GENERIC_MESSAGE;
}

/**
 * Code of any error thrown by an API call (non-HTTP errors give `null`). `notFound` is used for
 * a 404 without a ProblemDetails body (e.g. answered by a proxy).
 */
export function errorCode(error: unknown, notFound?: ErrorCode): ErrorCode | null {
  if (!(error instanceof HttpErrorResponse)) return null;
  return problemCode(error) ?? (error.status === 404 && notFound ? notFound : null);
}

/** Value of a ProblemDetails extension (e.g. `unavailableItems`), or `undefined`. */
export function problemExtension(error: unknown, name: string): unknown {
  if (!(error instanceof HttpErrorResponse)) return undefined;
  const body = problemBody(error) as Record<string, unknown> | null;
  return body?.[name];
}

/** First message per field of a `validation_failed` problem (`errors` extension, camelCase keys). */
export function problemFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof HttpErrorResponse)) return {};
  const body = problemBody(error);
  if (body === null || !('errors' in body)) return {};
  const errors = body.errors;
  if (typeof errors !== 'object' || errors === null) return {};
  const result: Record<string, string> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages) && typeof messages[0] === 'string') result[field] = messages[0];
  }
  return result;
}
