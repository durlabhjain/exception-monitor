<script lang="ts">
	import { toasts } from '$lib/stores/toast';
	import { CheckCircle, XCircle } from '@lucide/svelte';
</script>

<div class="fixed bottom-4 right-4 z-50 flex flex-col gap-2 pointer-events-none">
	{#each $toasts as toast (toast.id)}
		<div
			class="flex items-start gap-3 px-4 py-3 rounded-lg shadow-lg max-w-sm pointer-events-auto
			{toast.type === 'success'
				? 'bg-green-50 border border-green-200'
				: 'bg-red-50 border border-red-200'}"
		>
			{#if toast.type === 'success'}
				<CheckCircle size={18} class="text-green-500 shrink-0 mt-0.5" />
			{:else}
				<XCircle size={18} class="text-red-500 shrink-0 mt-0.5" />
			{/if}
			<p
				class="text-sm leading-snug {toast.type === 'success'
					? 'text-green-800'
					: 'text-red-800'}"
			>
				{toast.message}
			</p>
		</div>
	{/each}
</div>
