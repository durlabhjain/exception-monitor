<script lang="ts">
	let {
		open = false,
		title = 'Confirm',
		message = 'Are you sure?',
		confirmLabel = 'Confirm',
		danger = true,
		onConfirm,
		onCancel
	}: {
		open: boolean;
		title?: string;
		message?: string;
		confirmLabel?: string;
		danger?: boolean;
		onConfirm: () => void;
		onCancel: () => void;
	} = $props();
</script>

{#if open}
	<div class="fixed inset-0 z-50 overflow-y-auto">
		<div class="flex items-center justify-center min-h-screen px-4">
			<div
				class="fixed inset-0 bg-gray-500 bg-opacity-75"
				onclick={onCancel}
				role="presentation"
			></div>
			<div class="relative bg-white rounded-lg shadow-xl p-6 max-w-md w-full z-10">
				<h3 class="text-lg font-semibold text-gray-900 mb-2">{title}</h3>
				<p class="text-sm text-gray-500 mb-6">{message}</p>
				<div class="flex justify-end gap-3">
					<button
						onclick={onCancel}
						class="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
					>
						Cancel
					</button>
					<button
						onclick={onConfirm}
						class="px-4 py-2 text-sm font-medium text-white rounded-md
						{danger ? 'bg-red-600 hover:bg-red-700' : 'bg-indigo-600 hover:bg-indigo-700'}"
					>
						{confirmLabel}
					</button>
				</div>
			</div>
		</div>
	</div>
{/if}
