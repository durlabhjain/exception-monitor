<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { getClients } from '$lib/api/clients';
	import { getApplications, createApplication } from '$lib/api/applications';
	import Modal from '$lib/components/Modal.svelte';
	import { toasts } from '$lib/stores/toast';
	import { Plus, ChevronRight, ArrowLeft, AppWindow } from '@lucide/svelte';
	import type { Client, Application } from '$lib/types';

	let clientId = $derived($page.params.id);
	let client = $state<Client | null>(null);
	let apps = $state<Application[]>([]);
	let loading = $state(true);
	let showModal = $state(false);
	let saving = $state(false);

	let form = $state({
		name: '',
		slug: '',
		defaultEnvironment: 'production',
		defaultRetentionDays: 90
	});

	onMount(load);

	async function load() {
		loading = true;
		try {
			const [clients, applications] = await Promise.all([
				getClients(),
				getApplications(clientId)
			]);
			client = clients.find((c) => c.id === clientId) ?? null;
			apps = applications;
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load');
		} finally {
			loading = false;
		}
	}

	async function createApp() {
		if (!form.name.trim()) return;
		saving = true;
		try {
			await createApplication({
				clientId,
				name: form.name.trim(),
				slug: form.slug.trim() || undefined,
				defaultEnvironment: form.defaultEnvironment || 'production',
				defaultRetentionDays: form.defaultRetentionDays
			});
			toasts.success(`Application "${form.name}" created`);
			showModal = false;
			form = { name: '', slug: '', defaultEnvironment: 'production', defaultRetentionDays: 90 };
			await load();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to create application');
		} finally {
			saving = false;
		}
	}
</script>

<svelte:head><title>{client?.name ?? 'Client'} — Exception Monitor</title></svelte:head>

<div class="p-6">
	<a href="/clients" class="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-4">
		<ArrowLeft size={14} /> Clients
	</a>

	{#if loading}
		<div class="flex items-center gap-2 text-gray-500 text-sm">
			<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
			Loading...
		</div>
	{:else}
		<div class="flex items-start justify-between mb-6">
			<div>
				<h1 class="text-2xl font-bold text-gray-900">{client?.name ?? 'Unknown Client'}</h1>
				<p class="text-sm text-gray-500 font-mono mt-0.5">{client?.slug}</p>
			</div>
			<button
				onclick={() => (showModal = true)}
				class="flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700"
			>
				<Plus size={16} /> New Application
			</button>
		</div>

		{#if apps.length === 0}
			<div class="bg-white rounded-xl border border-dashed border-gray-300 py-20 text-center">
				<AppWindow size={36} class="text-gray-300 mx-auto mb-3" />
				<p class="text-gray-500 font-medium">No applications yet</p>
				<p class="text-gray-400 text-sm mt-1">Add an application to start ingesting errors</p>
				<button
					onclick={() => (showModal = true)}
					class="mt-4 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700"
				>
					New Application
				</button>
			</div>
		{:else}
			<div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
				<table class="w-full text-sm">
					<thead>
						<tr class="border-b border-gray-100 bg-gray-50/50">
							<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
							<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Environment</th>
							<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Retention</th>
							<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
							<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
							<th class="px-5 py-3"></th>
						</tr>
					</thead>
					<tbody class="divide-y divide-gray-50">
						{#each apps as app}
							<tr class="hover:bg-gray-50 transition-colors">
								<td class="px-5 py-3.5">
									<p class="font-medium text-gray-900">{app.name}</p>
									<p class="text-gray-400 font-mono text-xs">{app.slug}</p>
								</td>
								<td class="px-5 py-3.5 text-gray-600">{app.defaultEnvironment}</td>
								<td class="px-5 py-3.5 text-gray-600">{app.defaultRetentionDays}d</td>
								<td class="px-5 py-3.5">
									<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium
									{app.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}">
										{app.isActive ? 'Active' : 'Inactive'}
									</span>
								</td>
								<td class="px-5 py-3.5 text-gray-500">{new Date(app.createdAt).toLocaleDateString()}</td>
								<td class="px-5 py-3.5 text-right">
									<div class="flex items-center justify-end gap-3">
										<a href="/apps/{app.id}/errors" class="text-sm text-gray-500 hover:text-gray-700 font-medium">Errors</a>
										<a href="/apps/{app.id}" class="inline-flex items-center gap-1 text-indigo-600 hover:text-indigo-800 text-sm font-medium">
											Manage <ChevronRight size={14} />
										</a>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	{/if}
</div>

<Modal title="New Application" open={showModal} onClose={() => (showModal = false)}>
	<form onsubmit={(e) => { e.preventDefault(); createApp(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Name <span class="text-red-500">*</span></label>
				<input bind:value={form.name} required placeholder="My App" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Slug <span class="text-gray-400 text-xs">(optional)</span></label>
				<input bind:value={form.slug} placeholder="my-app" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			<div class="grid grid-cols-2 gap-4">
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Default Environment</label>
					<input bind:value={form.defaultEnvironment} placeholder="production" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Retention (days)</label>
					<input type="number" bind:value={form.defaultRetentionDays} min="1" max="3650" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
			</div>
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => (showModal = false)} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={saving || !form.name.trim()} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{saving ? 'Creating...' : 'Create Application'}
				</button>
			</div>
		</div>
	</form>
</Modal>
