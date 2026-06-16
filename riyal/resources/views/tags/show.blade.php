@extends('layouts.app')

@section('content')
    <header class="mb-8">
        <p class="text-sm font-semibold uppercase tracking-wide text-brand-700">{{ __('messages.tagged') }}</p>
        <h1 class="mt-1 text-3xl font-extrabold text-gray-900">#{{ $tag->name }}</h1>
    </header>

    @if ($posts->isEmpty())
        <p class="rounded-xl border border-dashed border-gray-300 bg-white p-8 text-center text-gray-500">
            {{ __('messages.nothing_here') }}
        </p>
    @else
        <div class="grid gap-6 md:grid-cols-3">
            @foreach ($posts as $post)
                <x-post-card :post="$post" />
            @endforeach
        </div>
        <div class="mt-8">{{ $posts->links() }}</div>
    @endif
@endsection
