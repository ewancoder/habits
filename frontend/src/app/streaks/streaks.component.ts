import { Component, computed, Signal, signal, WritableSignal } from '@angular/core';
import { HabitGroup, StreaksService } from '../streaks.service';
import { ThemeService } from '../theme.service';
import { HabitGroupsComponent } from '../habit-groups/habit-groups.component';
import { UiHabitGroup, UiHabitService } from '../state-management/ui-habit.service';
import { CachedHabitRepository } from '../state-management/cached.habit.repository';

export interface SelectedMonth {
    year: number;
    month: number;
}

@Component({
    selector: 'hab-streaks',
    imports: [HabitGroupsComponent],
    templateUrl: './streaks.component.html',
    styleUrl: './streaks.component.scss'
})
export class StreaksComponent {
    protected monthSignal: WritableSignal<SelectedMonth> = signal({
        year: new Date().getFullYear(),
        month: new Date().getMonth()
    });
    protected previousMonthSignal = computed(() => this.getPreviousMonth());
    protected nextMonthSignal = computed(() => this.getNextMonth());
    protected groupedHabitsSignal: Signal<UiHabitGroup[]>;

    constructor(
        private streaksService: StreaksService,
        private repo: CachedHabitRepository,
        uiHabitService: UiHabitService,
        protected themeService: ThemeService
    ) {
        this.groupedHabitsSignal = uiHabitService.groupedHabitsSignal;
    }

    protected createHabit() {
        const habit = prompt('Habit name');
        const lengthDays = prompt('Length of streak (for example 2 means you need to do it every other day)');
        const group = prompt('Group') ?? undefined;

        if (habit && lengthDays) {
            this.repo
                .create({
                    name: habit,
                    lengthDays: +lengthDays,
                    group: group
                })
                .subscribe();
        }
    }

    protected moveBack() {
        this.monthSignal.set(this.getPreviousMonth());
    }

    protected moveForward() {
        this.monthSignal.set(this.getNextMonth());
    }

    private getPreviousMonth() {
        const current = this.monthSignal();
        const previousYear = current.month === 0 ? current.year - 1 : current.year;
        const previousMonth = current.month === 0 ? 11 : current.month - 1;

        return {
            year: previousYear,
            month: previousMonth
        };
    }

    private getNextMonth() {
        const current = this.monthSignal();
        const nextYear = current.month === 11 ? current.year + 1 : current.year;
        const nextMonth = current.month === 11 ? 0 : current.month + 1;

        return {
            year: nextYear,
            month: nextMonth
        };
    }
}
