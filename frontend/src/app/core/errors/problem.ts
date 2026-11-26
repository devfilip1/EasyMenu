import { HttpErrorResponse } from '@angular/common/http';
import { ErrorCode } from '../../api/generated/models/error-code';
import { ERROR_CODE } from '../../api/generated/models/error-code-array';

const MESSAGES: Record<ErrorCode, string> = {
  validation_failed: 'Confira os campos destacados e tente novamente.',
  invalid_credentials: 'E-mail ou senha incorretos.',
  email_taken: 'Este e-mail já está cadastrado.',
  slug_taken: 'Este endereço de cardápio já está em uso. Escolha outro.',
  invalid_slug: 'Endereço de cardápio inválido. Use letras minúsculas, números e hífens.',
  not_found: 'Não encontramos o que você procurava.',
  category_not_found: 'Categoria não encontrada.',
  category_not_empty: 'Esta categoria ainda tem itens. Mova ou exclua os itens antes.',
  invalid_order_list:
    'A nova ordem não confere com a lista atual. Atualize a página e tente de novo.',
  invalid_price: 'Informe um preço maior que zero.',
  invalid_promotional_price: 'O preço promocional deve ser menor que o preço normal.',
  invalid_image_type: 'Formato de imagem não suportado. Envie JPG, PNG ou WebP.',
  image_too_large: 'A imagem é grande demais. Envie uma foto menor.',
  establishment_not_found: 'Estabelecimento não encontrado.',
  empty_cart: 'Seu carrinho está vazio.',
  invalid_quantity: 'Quantidade inválida.',
  items_unavailable: 'Alguns itens do carrinho não estão mais disponíveis.',
  payment_declined: 'Pagamento recusado. Tente outro meio de pagamento.',
  order_not_found: 'Pedido não encontrado.',
  invalid_status_transition: 'Não é possível mudar o pedido para esse status agora.',
  order_not_ready: 'O pedido ainda não está pronto para retirada.',
  rate_limited: 'Muitas tentativas seguidas. Aguarde um pouco e tente novamente.',
  payload_too_large: 'O envio excedeu o tamanho máximo permitido.',
};

const GENERIC_MESSAGE = 'Algo deu errado. Tente novamente em instantes.';

const KNOWN_CODES: ReadonlySet<string> = new Set(ERROR_CODE);

/** Extracts the stable `code` extension from a ProblemDetails error response, or `null`. */
export function problemCode(error: HttpErrorResponse): ErrorCode | null {
  const body: unknown = error.error;
  if (typeof body !== 'object' || body === null || !('code' in body)) return null;
  const { code } = body;
  return typeof code === 'string' && KNOWN_CODES.has(code) ? (code as ErrorCode) : null;
}

/** pt-BR message for an `ErrorCode`; a generic message when the code is unknown or absent. */
export function problemMessage(code: ErrorCode | null | undefined): string {
  return (code && MESSAGES[code]) || GENERIC_MESSAGE;
}
