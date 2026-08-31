import { Wordmark } from './components/Wordmark'
import { SessionStatus } from './features/auth/SessionStatus'

function App() {
  return (
    <main className="grid min-h-screen bg-canvas lg:place-items-center lg:p-8 xl:p-12">
      <div className="grid min-h-screen w-full overflow-hidden bg-surface lg:min-h-[calc(100vh-4rem)] lg:max-w-7xl lg:grid-cols-[0.94fr_1.06fr] lg:rounded-panel lg:shadow-panel xl:min-h-[calc(100vh-6rem)]">
        <aside className="auth-brand-panel relative hidden overflow-hidden p-14 text-white lg:flex lg:flex-col xl:p-16">
          <div aria-hidden="true" className="auth-brand-beams">
            <span className="auth-brand-beam auth-brand-beam-one" />
            <span className="auth-brand-beam auth-brand-beam-two" />
            <span className="auth-brand-beam auth-brand-beam-three" />
          </div>

          <div className="auth-brand-enter relative z-10">
            <Wordmark inverse />
          </div>

          <div className="auth-brand-enter relative z-10 my-auto max-w-md">
            <p className="text-4xl font-semibold tracking-[-0.035em] text-balance xl:text-5xl xl:leading-[1.08]">
              Workforce operations, kept organised.
            </p>
            <p className="mt-6 max-w-sm text-lg leading-8 text-blue-100/85">
              Manage people, assignments, approvals and day-to-day operations
              from one place.
            </p>
          </div>
        </aside>

        <section
          aria-label="Account access"
          className="flex min-w-0 bg-surface px-6 py-10 sm:px-10 sm:py-12 lg:items-center lg:px-16 xl:px-24"
        >
          <div className="auth-form-enter relative mx-auto w-full max-w-116 lg:-top-5">
            <div className="mb-14 lg:hidden">
              <Wordmark />
            </div>
            <SessionStatus />
          </div>
        </section>
      </div>
    </main>
  )
}

export default App
