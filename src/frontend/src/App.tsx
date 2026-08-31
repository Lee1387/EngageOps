import { SessionStatus } from './features/auth/SessionStatus'

const workflow = ['Organisation', 'Clients', 'Workers', 'Assignments'] as const

function App() {
  return (
    <main className="auth-shell relative min-h-screen overflow-hidden text-slate-950">
      <div className="auth-grid pointer-events-none absolute inset-0" />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -top-28 -left-28 size-96 rounded-full bg-blue-300/30 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -right-32 -bottom-36 size-96 rounded-full bg-cyan-200/40 blur-3xl"
      />

      <div className="relative mx-auto grid min-h-screen w-full max-w-7xl items-center gap-14 px-6 py-12 lg:grid-cols-[1.08fr_0.92fr] lg:px-12 lg:py-16 xl:gap-20">
        <section className="auth-enter max-w-2xl min-w-0">
          <div className="flex items-center gap-3">
            <div className="brand-mark" aria-hidden="true">
              <svg viewBox="0 0 32 32" className="size-7" fill="none">
                <path
                  d="M9 8.5h11.5a4 4 0 0 1 0 8H13a4 4 0 0 0 0 8h10"
                  stroke="currentColor"
                  strokeWidth="3"
                  strokeLinecap="round"
                />
                <circle cx="9" cy="8.5" r="2" fill="currentColor" />
                <circle cx="23" cy="24.5" r="2" fill="currentColor" />
              </svg>
            </div>
            <span className="text-sm font-bold tracking-[0.18em] text-slate-950 uppercase">
              EngageOps
            </span>
          </div>

          <div className="mt-9 inline-flex items-center gap-2 rounded-full border border-blue-200/80 bg-white/75 px-3.5 py-2 text-xs font-semibold text-blue-800 shadow-sm backdrop-blur-sm">
            <span className="size-1.5 rounded-full bg-blue-600 shadow-[0_0_0_4px_rgba(37,99,235,0.12)]" />
            Built for modern workforce operations
          </div>

          <h1 className="mt-6 text-4xl font-semibold tracking-[-0.04em] text-balance text-slate-950 sm:text-5xl lg:text-6xl lg:leading-[1.05]">
            Contractor operations,{' '}
            <span className="text-gradient">kept connected.</span>
          </h1>
          <p className="mt-6 max-w-xl text-lg leading-8 text-slate-600">
            Bring clients, workers and assignments into one clear operational
            workspace built for growing teams.
          </p>

          <div className="mt-10 max-w-2xl rounded-2xl border border-white/90 bg-white/65 p-4 shadow-lg shadow-blue-950/5 backdrop-blur-md sm:p-5">
            <div className="mb-4 flex items-center justify-between gap-4">
              <p className="text-xs font-semibold tracking-[0.14em] text-slate-500 uppercase">
                One connected workflow
              </p>
              <p className="hidden text-xs font-medium text-blue-700 sm:block">
                Assignment-led operations
              </p>
            </div>
            <ol
              aria-label="EngageOps operational workflow"
              className="grid grid-cols-2 gap-2 sm:grid-cols-4"
            >
              {workflow.map((step, index) => (
                <li
                  className={`workflow-step ${index === workflow.length - 1 ? 'workflow-step-active' : ''}`}
                  key={step}
                >
                  <span className="text-[0.65rem] font-bold tracking-wider opacity-60">
                    {String(index + 1).padStart(2, '0')}
                  </span>
                  <span className="mt-1 block text-sm font-semibold">
                    {step}
                  </span>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section
          aria-label="Account access"
          className="auth-enter auth-enter-delayed relative w-full min-w-0 overflow-hidden rounded-3xl border border-white bg-white/95 p-7 shadow-[0_28px_80px_-28px_rgba(15,23,42,0.3)] ring-1 ring-slate-200/70 backdrop-blur-xl sm:p-10 lg:justify-self-end"
        >
          <div
            aria-hidden="true"
            className="absolute inset-x-0 top-0 h-1 bg-linear-to-r from-blue-700 via-blue-500 to-cyan-400"
          />
          <SessionStatus />
        </section>
      </div>
    </main>
  )
}

export default App
