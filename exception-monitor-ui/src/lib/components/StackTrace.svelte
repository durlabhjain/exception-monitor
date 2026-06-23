<script lang="ts">
	import { Copy, Check } from '@lucide/svelte';

	let { value }: { value: string } = $props();
	let copied = $state(false);

	async function copy() {
		await navigator.clipboard.writeText(value);
		copied = true;
		setTimeout(() => (copied = false), 2000);
	}
</script>

<div class="relative">
	<button
		onclick={copy}
		class="absolute top-2 right-2 p-1.5 rounded text-gray-400 hover:text-white bg-gray-700 hover:bg-gray-600 transition-colors z-10"
		title="Copy stack trace"
	>
		{#if copied}
			<Check size={13} class="text-green-400" />
		{:else}
			<Copy size={13} />
		{/if}
	</button>
	<pre
		class="bg-gray-900 text-gray-100 text-xs p-4 pr-12 rounded-lg overflow-x-auto overflow-y-auto max-h-96 font-mono whitespace-pre-wrap break-all leading-relaxed"
	>{value}</pre>
</div>
