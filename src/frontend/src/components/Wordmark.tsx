interface WordmarkProps {
  inverse?: boolean
}

export function Wordmark({ inverse = false }: WordmarkProps) {
  return (
    <span
      className={`inline-flex text-[1.35rem] font-semibold tracking-[-0.035em] ${inverse ? 'text-white' : 'text-brand-950'}`}
    >
      EngageOps
    </span>
  )
}
