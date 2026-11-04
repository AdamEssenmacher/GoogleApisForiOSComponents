using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Foundation;

namespace Firebase.CloudFirestore
{
	// Binding-definition interfaces cannot satisfy NSDictionary's INativeObject
	// constraint before bgen runs. Keep raw initializers internal, and expose the
	// native expression-valued contract through these generated-class constructors.
	public partial class LiteralsSourceStageBridge
	{
		public LiteralsSourceStageBridge (NSDictionary<NSString, ExprBridge> [] data)
			: this ((NSDictionary []) (data ?? throw new ArgumentNullException (nameof (data))))
		{
		}
	}

	public partial class UpdateStageBridge
	{
		public UpdateStageBridge (NSDictionary<NSString, ExprBridge> fields)
			: this ((NSDictionary) (fields ?? throw new ArgumentNullException (nameof (fields))))
		{
		}
	}

	public partial class UpsertStageBridge
	{
		public UpsertStageBridge (NSDictionary<NSString, ExprBridge> additionalFields, string? collectionPath, ExprBridge? documentIdExpression)
			: this ((NSDictionary) (additionalFields ?? throw new ArgumentNullException (nameof (additionalFields))), collectionPath, documentIdExpression)
		{
		}
	}

	public partial class CollectionSourceStageBridge
	{
		// Preserve the pre-12.14 managed overload using the new native initializer.
		public CollectionSourceStageBridge (CollectionReference reference, Firestore db) : this (reference, db, null)
		{
		}
	}

	public partial class CollectionGroupSourceStageBridge
	{
		public CollectionGroupSourceStageBridge (string collectionId) : this (collectionId, null)
		{
		}
	}

	public partial class FunctionExprBridge
	{
		// Firebase 12.12 adds nullable native options. Keep the existing managed overload.
		public FunctionExprBridge (string name, ExprBridge [] args) : this (name, args, null)
		{
		}
	}

	class CloudFirestoreHelper
	{
		public static NSObject []? GetNSObjects (object []? objects)
		{
			if (objects == null)
				return null;

			var nsObjects = new NSObject [objects.Length];

			for (int i = 0; i < objects.Length; i++)
				nsObjects [i] = NSObject.FromObject (objects [i]);

			return nsObjects;
		}
	}

	public partial class Firestore
	{
		// id  _Nullable (^ _Nonnull)(FIRTransaction * _Nonnull, NSError * _Nullable * _Nullable)
		public delegate NSObject? TransactionUpdateHandler (Transaction transaction, ref NSError? error);

		public void RunTransaction (TransactionUpdateHandler updateHandler, TransactionCompletionHandler completion)
		{
			if (updateHandler == null)
				throw new ArgumentNullException (nameof (updateHandler));

			_RunTransaction (InternalUpdateHandler, completion);

			NSObject? InternalUpdateHandler (Transaction transaction, IntPtr pError)
			{
				NSError? error = null;
				var result = updateHandler (transaction, ref error);

				if (pError != IntPtr.Zero && error != null)
					Marshal.WriteIntPtr (pError, error.Handle);

				return result;
			}
		}

		public void RunTransaction (TransactionOptions options, TransactionUpdateHandler updateHandler, TransactionCompletionHandler completion)
		{
			if (updateHandler == null)
				throw new ArgumentNullException (nameof (updateHandler));

			_RunTransaction (options, InternalUpdateHandler, completion);

			NSObject? InternalUpdateHandler (Transaction transaction, IntPtr pError)
			{
				NSError? error = null;
				var result = updateHandler (transaction, ref error);

				if (pError != IntPtr.Zero && error != null)
					Marshal.WriteIntPtr (pError, error.Handle);

				return result;
			}
		}

		public Task<NSObject?> RunTransactionAsync (TransactionUpdateHandler updateHandler)
		{
			var tcs = new TaskCompletionSource<NSObject?> ();
			RunTransaction (updateHandler, (result_, error_) => {
				if (error_ != null)
					tcs.SetException (new NSErrorException (error_));
				else
					tcs.SetResult (result_);
			});
			return tcs.Task;
		}

		public Task<NSObject?> RunTransactionAsync (TransactionOptions options, TransactionUpdateHandler updateHandler)
		{
			var tcs = new TaskCompletionSource<NSObject?> ();
			RunTransaction (options, updateHandler, (result_, error_) => {
				if (error_ != null)
					tcs.SetException (new NSErrorException (error_));
				else
					tcs.SetResult (result_);
			});
			return tcs.Task;
		}
	}
}
