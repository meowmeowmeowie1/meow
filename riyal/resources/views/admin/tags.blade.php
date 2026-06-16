@extends('admin.layout')

@section('admin')
<h1 class="text-2xl font-bold">Tags</h1>

<div class="mt-6 grid gap-6 lg:grid-cols-3">
    <form action="{{ route('admin.tags.store') }}" method="post" class="space-y-3 rounded-xl border border-gray-200 bg-white p-5">
        @csrf
        <h2 class="font-semibold">New tag</h2>
        <select name="locale" class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
            <option value="ar">العربية (AR)</option>
            <option value="en">English (EN)</option>
        </select>
        <input name="name" placeholder="Name" required class="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
        <button class="w-full rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700">Add</button>
    </form>

    <div class="lg:col-span-2 overflow-hidden rounded-xl border border-gray-200 bg-white">
        <table class="w-full text-sm">
            <thead class="bg-gray-50 text-xs uppercase text-gray-500">
                <tr><th class="px-4 py-3 text-start">Name</th><th class="px-4 py-3 text-start">Locale</th><th class="px-4 py-3 text-start">Posts</th><th class="px-4 py-3"></th></tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
                @forelse ($tags as $tag)
                    <tr>
                        <td class="px-4 py-3 font-medium">{{ $tag->name }} <span class="text-xs text-gray-400">/{{ $tag->slug }}</span></td>
                        <td class="px-4 py-3 uppercase">{{ $tag->locale }}</td>
                        <td class="px-4 py-3">{{ $tag->posts_count }}</td>
                        <td class="px-4 py-3 text-end">
                            <form action="{{ route('admin.tags.destroy', $tag) }}" method="post" onsubmit="return confirm('Delete tag?')">
                                @csrf @method('DELETE')
                                <button class="text-xs text-red-600 hover:underline">Delete</button>
                            </form>
                        </td>
                    </tr>
                @empty
                    <tr><td colspan="4" class="px-4 py-6 text-center text-gray-500">No tags yet.</td></tr>
                @endforelse
            </tbody>
        </table>
    </div>
</div>
@endsection
