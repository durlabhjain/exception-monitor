<script lang="ts">
	import type { Snippet } from 'svelte';
	import { X } from '@lucide/svelte';

	let {
		title,
		open = false,
		onClose,
		children,
		wide = false
	}: {
		title: string;
		open: boolean;
		onClose: () => void;
		children: Snippet;
		wide?: boolean;
	} = $props();
</script>

{#if open}
	<div class="fixed inset-0 z-50 overflow-y-auto">
		<div class="flex items-center justify-center min-h-screen px-4 pt-4 pb-20">
			<div
				class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
				onclick={onClose}
				role="presentation"
			></div>
			<div
				class="relative bg-white rounded-lg shadow-xl w-full {wide
					? 'max-w-2xl'
					: 'max-w-lg'} z-10"
			>
				<div class="px-6 pt-5 pb-6">
					<div class="flex items-center justify-between mb-5">
						<h3 class="text-lg font-semibold text-gray-900">{title}</h3>
						<button
							onclick={onClose}
							class="text-gray-400 hover:text-gray-600 transition-colors"
						>
							<X size={20} />
						</button>
					</div>
					{@render children()}
				</div>
			</div>
		</div>
	</div>
{/if}
