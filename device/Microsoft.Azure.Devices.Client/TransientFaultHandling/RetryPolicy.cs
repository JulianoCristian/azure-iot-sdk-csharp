using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling
{
    /// <summary>
    /// Provides the base implementation of the retry mechanism for unreliable actions and transient conditions.
    /// </summary>
    public class RetryPolicy
    {
        /// <summary>
        /// Implements a strategy that ignores any transient errors.
        /// </summary>
        private sealed class TransientErrorIgnoreStrategy : ITransientErrorDetectionStrategy
        {
            /// <summary>
            /// Always returns false.
            /// </summary>
            /// <param name="ex">The exception.</param>
            /// <returns>Always false.</returns>
            public bool IsTransient(Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Implements a strategy that treats all exceptions as transient errors.
        /// </summary>
        private sealed class TransientErrorCatchAllStrategy : ITransientErrorDetectionStrategy
        {
            /// <summary>
            /// Always returns true.
            /// </summary>
            /// <param name="ex">The exception.</param>
            /// <returns>Always true.</returns>
            public bool IsTransient(Exception ex)
            {
                return true;
            }
        }

        private static RetryPolicy noRetry = new RetryPolicy(new RetryPolicy.TransientErrorIgnoreStrategy(), RetryStrategy.NoRetry);

        private static RetryPolicy defaultFixed = new RetryPolicy(new RetryPolicy.TransientErrorCatchAllStrategy(), RetryStrategy.DefaultFixed);

        private static RetryPolicy defaultProgressive = new RetryPolicy(new RetryPolicy.TransientErrorCatchAllStrategy(), RetryStrategy.DefaultProgressive);

        private static RetryPolicy defaultExponential = new RetryPolicy(new RetryPolicy.TransientErrorCatchAllStrategy(), RetryStrategy.DefaultExponential);

        /// <summary>
        /// An instance of a callback delegate that will be invoked whenever a retry condition is encountered.
        /// </summary>
        public event EventHandler<RetryingEventArgs> Retrying;

        /// <summary>
        /// Returns a default policy that performs no retries, but invokes the action only once.
        /// </summary>
        public static RetryPolicy NoRetry
        {
            get
            {
                return RetryPolicy.noRetry;
            }
        }

        /// <summary>
        /// Returns a default policy that implements a fixed retry interval configured with the default <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.FixedInterval" /> retry strategy.
        /// The default retry policy treats all caught exceptions as transient errors.
        /// </summary>
        public static RetryPolicy DefaultFixed
        {
            get
            {
                return RetryPolicy.defaultFixed;
            }
        }

        /// <summary>
        /// Returns a default policy that implements a progressive retry interval configured with the default <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.Incremental" /> retry strategy.
        /// The default retry policy treats all caught exceptions as transient errors.
        /// </summary>
        public static RetryPolicy DefaultProgressive
        {
            get
            {
                return RetryPolicy.defaultProgressive;
            }
        }

        /// <summary>
        /// Returns a default policy that implements a random exponential retry interval configured with the default <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.FixedInterval" /> retry strategy.
        /// The default retry policy treats all caught exceptions as transient errors.
        /// </summary>
        public static RetryPolicy DefaultExponential
        {
            get
            {
                return RetryPolicy.defaultExponential;
            }
        }

        /// <summary>
        /// Gets the retry strategy.
        /// </summary>
        public RetryStrategy RetryStrategy
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the instance of the error detection strategy.
        /// </summary>
        public ITransientErrorDetectionStrategy ErrorDetectionStrategy
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryPolicy" /> class with the specified number of retry attempts and parameters defining the progressive delay between retries.
        /// </summary>
        /// <param name="errorDetectionStrategy">The <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ITransientErrorDetectionStrategy" /> that is responsible for detecting transient conditions.</param>
        /// <param name="retryStrategy">The strategy to use for this retry policy.</param>
        public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, RetryStrategy retryStrategy)
        {
            Guard.ArgumentNotNull(errorDetectionStrategy, "errorDetectionStrategy");
            Guard.ArgumentNotNull(retryStrategy, "retryPolicy");
            this.ErrorDetectionStrategy = errorDetectionStrategy;
            if (errorDetectionStrategy == null)
            {
                throw new InvalidOperationException("The error detection strategy type must implement the ITransientErrorDetectionStrategy interface.");
            }
            this.RetryStrategy = retryStrategy;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryPolicy" /> class with the specified number of retry attempts and default fixed time interval between retries.
        /// </summary>
        /// <param name="errorDetectionStrategy">The <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ITransientErrorDetectionStrategy" /> that is responsible for detecting transient conditions.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount) : this(errorDetectionStrategy, new FixedInterval(retryCount))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryPolicy" /> class with the specified number of retry attempts and fixed time interval between retries.
        /// </summary>
        /// <param name="errorDetectionStrategy">The <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ITransientErrorDetectionStrategy" /> that is responsible for detecting transient conditions.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="retryInterval">The interval between retries.</param>
        public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount, TimeSpan retryInterval) : this(errorDetectionStrategy, new FixedInterval(retryCount, retryInterval))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryPolicy" /> class with the specified number of retry attempts and backoff parameters for calculating the exponential delay between retries.
        /// </summary>
        /// <param name="errorDetectionStrategy">The <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ITransientErrorDetectionStrategy" /> that is responsible for detecting transient conditions.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="minBackoff">The minimum backoff time.</param>
        /// <param name="maxBackoff">The maximum backoff time.</param>
        /// <param name="deltaBackoff">The time value that will be used to calculate a random delta in the exponential delay between retries.</param>
        public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff) : this(errorDetectionStrategy, new ExponentialBackoff(retryCount, minBackoff, maxBackoff, deltaBackoff))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryPolicy" /> class with the specified number of retry attempts and parameters defining the progressive delay between retries.
        /// </summary>
        /// <param name="errorDetectionStrategy">The <see cref="T:Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.ITransientErrorDetectionStrategy" /> that is responsible for detecting transient conditions.</param>
        /// <param name="retryCount">The number of retry attempts.</param>
        /// <param name="initialInterval">The initial interval that will apply for the first retry.</param>
        /// <param name="increment">The incremental time value that will be used to calculate the progressive delay between retries.</param>
        public RetryPolicy(ITransientErrorDetectionStrategy errorDetectionStrategy, int retryCount, TimeSpan initialInterval, TimeSpan increment) : this(errorDetectionStrategy, new Incremental(retryCount, initialInterval, increment))
        {
        }

        /// <summary>
        /// Repetitively executes the specified action while it satisfies the current retry policy.
        /// </summary>
        /// <param name="action">A delegate that represents the executable action that doesn't return any results.</param>
        public virtual void ExecuteAction(Action action)
        {
            Guard.ArgumentNotNull(action, "action");
            this.ExecuteAction<object>(delegate
            {
                action();
                return null;
            });
        }

        /// <summary>
        /// Repetitively executes the specified action while it satisfies the current retry policy.
        /// </summary>
        /// <typeparam name="TResult">The type of result expected from the executable action.</typeparam>
        /// <param name="func">A delegate that represents the executable action that returns the result of type <typeparamref name="TResult" />.</param>
        /// <returns>The result from the action.</returns>
        public virtual TResult ExecuteAction<TResult>(Func<TResult> func)
        {
            Guard.ArgumentNotNull(func, "func");
            int num = 0;
            TimeSpan zero = TimeSpan.Zero;
            ShouldRetry shouldRetry = this.RetryStrategy.GetShouldRetry();
            TResult result;
            while (true)
            {
                Exception ex = null;
                try
                {
                    result = func();
                    break;
                }
                catch (RetryLimitExceededException ex2)
                {
                    if (ex2.InnerException != null)
                    {
                        throw ex2.InnerException;
                    }
                    result = default(TResult);
                    break;
                }
                catch (Exception ex3)
                {
                    ex = ex3;
                    if (!this.ErrorDetectionStrategy.IsTransient(ex) || !shouldRetry(num++, ex, out zero))
                    {
                        throw;
                    }
                }
                if (zero.TotalMilliseconds < 0.0)
                {
                    zero = TimeSpan.Zero;
                }
                this.OnRetrying(num, ex, zero);
                if (num > 1 || !this.RetryStrategy.FastFirstRetry)
                {
                    Task.Delay(zero).Wait();
                }
            }
            return result;
        }

        /// <summary>
        /// Repetitively executes the specified asynchronous task while it satisfies the current retry policy.
        /// </summary>
        /// <param name="taskAction">A function that returns a started task (also known as "hot" task).</param>
        /// <returns>
        /// A task that will run to completion if the original task completes successfully (either the
        /// first time or after retrying transient failures). If the task fails with a non-transient error or
        /// the retry limit is reached, the returned task will transition to a faulted state and the exception must be observed.
        /// </returns>
        public Task ExecuteAsync(Func<Task> taskAction)
        {
            return this.ExecuteAsync(taskAction, default(CancellationToken));
        }

        /// <summary>
        /// Repetitively executes the specified asynchronous task while it satisfies the current retry policy.
        /// </summary>
        /// <param name="taskAction">A function that returns a started task (also known as "hot" task).</param>
        /// <param name="cancellationToken">The token used to cancel the retry operation. This token does not cancel the execution of the asynchronous task.</param>
        /// <returns>
        /// Returns a task that will run to completion if the original task completes successfully (either the
        /// first time or after retrying transient failures). If the task fails with a non-transient error or
        /// the retry limit is reached, the returned task will transition to a faulted state and the exception must be observed.
        /// </returns>
        public Task ExecuteAsync(Func<Task> taskAction, CancellationToken cancellationToken)
        {
            if (taskAction == null)
            {
                throw new ArgumentNullException("taskAction");
            }
            return new AsyncExecution(taskAction, this.RetryStrategy.GetShouldRetry(), new Func<Exception, bool>(this.ErrorDetectionStrategy.IsTransient), new Action<int, Exception, TimeSpan>(this.OnRetrying), this.RetryStrategy.FastFirstRetry, cancellationToken).ExecuteAsync();
        }

        /// <summary>
        /// Repeatedly executes the specified asynchronous task while it satisfies the current retry policy.
        /// </summary>
        /// <param name="taskFunc">A function that returns a started task (also known as "hot" task).</param>
        /// <returns>
        /// Returns a task that will run to completion if the original task completes successfully (either the
        /// first time or after retrying transient failures). If the task fails with a non-transient error or
        /// the retry limit is reached, the returned task will transition to a faulted state and the exception must be observed.
        /// </returns>
        public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> taskFunc)
        {
            return this.ExecuteAsync<TResult>(taskFunc, default(CancellationToken));
        }

        /// <summary>
        /// Repeatedly executes the specified asynchronous task while it satisfies the current retry policy.
        /// </summary>
        /// <param name="taskFunc">A function that returns a started task (also known as "hot" task).</param>
        /// <param name="cancellationToken">The token used to cancel the retry operation. This token does not cancel the execution of the asynchronous task.</param>
        /// <returns>
        /// Returns a task that will run to completion if the original task completes successfully (either the
        /// first time or after retrying transient failures). If the task fails with a non-transient error or
        /// the retry limit is reached, the returned task will transition to a faulted state and the exception must be observed.
        /// </returns>
        public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> taskFunc, CancellationToken cancellationToken)
        {
            if (taskFunc == null)
            {
                throw new ArgumentNullException("taskFunc");
            }
            return new AsyncExecution<TResult>(taskFunc, this.RetryStrategy.GetShouldRetry(), new Func<Exception, bool>(this.ErrorDetectionStrategy.IsTransient), new Action<int, Exception, TimeSpan>(this.OnRetrying), this.RetryStrategy.FastFirstRetry, cancellationToken).ExecuteAsync();
        }

        /// <summary>
        /// Notifies the subscribers whenever a retry condition is encountered.
        /// </summary>
        /// <param name="retryCount">The current retry attempt count.</param>
        /// <param name="lastError">The exception that caused the retry conditions to occur.</param>
        /// <param name="delay">The delay that indicates how long the current thread will be suspended before the next iteration is invoked.</param>
        protected virtual void OnRetrying(int retryCount, Exception lastError, TimeSpan delay)
        {
            if (this.Retrying != null)
            {
                this.Retrying(this, new RetryingEventArgs(retryCount, delay, lastError));
            }
        }
    }
}
