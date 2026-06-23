<script lang="ts">
	import { onMount } from 'svelte';
	import { getClients, createClient } from '$lib/api/clients';
	import Modal from '$lib/components/Modal.svelte';
	import { toasts } from '$lib/stores/toast';
	import { Plus, ChevronRight, Building2 } from '@lucide/svelte';
	import type { Client } from '$lib/types';

	let clients = $state<Client[]>([]);
	let loading = $state(true);
	let showModal = $state(false);
	let name = $state('');
	let slug = $state('');
	let saving = $state(false);

	onMount(load);

	async function load() {
		loading = true;
		try {
			clients = await getClients();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load clients');
		} finally {
			loading = false;
		}
	}

	async function create() {
		if (!name.trim()) return;
		saving = true;
		try {
			await createClient({ name: name.trim(), slug: slug.trim() || undefined });
			toasts.success(`Client "${name}" created`);
			showModal = false;
			name = '';
			slug = '';
			await load();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to create client');
		} finally {
			saving = false;
		}
	}
</script>

<svelte:head><title>Clients — Exception Monitor</title></svelte:head>

<div class="p-6">
	<div class="flex items-center justify-between mb-6">
		<div>
			<h1 class="text-2xl font-bold text-gray-900">Clients</h1>
			<p class="text-sm text-gray-500 mt-0.5">Manage tenants and their applications</p>
		</div>
		<button
			onclick={() => (showModal = true)}
			class="flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700 transition-colors"
		>
			<Plus size={16} /> New Client
		</button>
	</div>

	{#if loading}
		<div class="flex items-center gap-2 text-gray-500 text-sm">
			<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
			Loading...
		</div>
	{:else if clients.length === 0}
		<div class="bg-white rounded-xl border border-dashed border-gray-300 py-20 text-center">
			<Building2 size={36} class="text-gray-300 mx-auto mb-3" />
			<p class="text-gray-500 font-medium">No clients yet</p>
			<p class="text-gray-400 text-sm mt-1">Create your first client to get started</p>
			<button
				onclick={() => (showModal = true)}
				class="mt-4 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700"
			>
				New Client
			</button>
		</div>
	{:else}
		<div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
			<table class="w-full text-sm">
				<thead>
					<tr class="border-b border-gray-100 bg-gray-50/50">
						<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
						<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Slug</th>
						<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
						<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
						<th class="px-5 py-3"></th>
					</tr>
				</thead>
				<tbody class="divide-y divide-gray-50">
					{#each clients as client}
						<tr class="hover:bg-gray-50 transition-colors">
							<td class="px-5 py-3.5 font-medium text-gray-900">{client.name}</td>
							<td class="px-5 py-3.5 font-mono text-xs text-gray-500">{client.slug}</td>
							<td class="px-5 py-3.5">
								<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium
								{client.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}">
									{client.isActive ? 'Active' : 'Inactive'}
								</span>
							</td>
							<td class="px-5 py-3.5 text-gray-500">{new Date(client.createdAt).toLocaleDateString()}</td>
							<td class="px-5 py-3.5 text-right">
								<a
									href="/clients/{client.id}"
									class="inline-flex items-center gap-1 text-indigo-600 hover:text-indigo-800 text-sm font-medium"
								>
									View apps <ChevronRight size={14} />
								</a>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</div>

<Modal title="New Client" open={showModal} onClose={() => (showModal = false)}>
	<form onsubmit={(e) => { e.preventDefault(); create(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">
					Name <span class="text-red-500">*</span>
				</label>
				<input
					bind:value={name}
					required
					placeholder="Acme Corp"
					class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
				/>
			</div>
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">
					Slug <span class="text-gray-400 text-xs">(optional, auto-generated)</span>
				</label>
				<input
					bind:value={slug}
					placeholder="acme-corp"
					class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
				/>
			</div>
			<div class="flex justify-end gap-3 pt-1">
				<button
					type="button"
					onclick={() => (showModal = false)}
					class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50"
				>
					Cancel
				</button>
				<button
					type="submit"
					disabled={saving || !name.trim()}
					class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
				>
					{saving ? 'Creating...' : 'Create Client'}
				</button>
			</div>
		</div>
	</form>
</Modal>
